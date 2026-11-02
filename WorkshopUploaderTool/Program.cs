using System;
using System.IO;
using System.Threading;
using Steamworks;

namespace WorkshopUploaderTool
{
    /// <summary>
    /// 独立创意工坊上传工具（steamcmd 的替代品）。
    /// 以当前登录的 Steam 用户身份，对指定 AppID 创建/更新创意工坊物品。
    /// 用法:
    ///   WorkshopUploaderTool --content <dir> [--title t] [--desc-file f] [--desc d]
    ///                        [--id <publishedFileId>] [--preview <img>]
    ///                        [--changenote n] [--visibility public|unlisted|private]
    /// 需求: Steam 客户端已运行且已登录；本目录有 steam_appid.txt 与 steam_api64.dll。
    /// </summary>
    internal static class Program
    {
        private static PublishedFileId_t _itemId;
        private static UGCUpdateHandle_t _handle;
        private static CallResult<CreateItemResult_t> _createCall;
        private static CallResult<SubmitItemUpdateResult_t> _submitCall;
        private static string _title = "Gamepad Support (Steam Deck 测试)";
        private static string _desc = "";
        private static string _changenote = "update";
        private static string _contentDir;
        private static string _previewFile;
        private static ERemoteStoragePublishedFileVisibility _visibility =
            ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic;
        private static ulong _deleteId;
        private static readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        private static bool _ok;

        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Console.WriteLine("=== Probably Stolen 创意工坊上传工具 ===");

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--content": _contentDir = args[++i]; break;
                    case "--title": _title = args[++i]; break;
                    case "--desc": _desc = args[++i]; break;
                    case "--desc-file": _desc = File.ReadAllText(args[++i]); break;
                    case "--id": _itemId = new PublishedFileId_t(ulong.Parse(args[++i])); break;
                    case "--preview": _previewFile = args[++i]; break;
                    case "--changenote": _changenote = args[++i]; break;
                    case "--visibility":
                        _visibility = args[++i].ToLowerInvariant() switch
                        {
                            "public" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
                            "unlisted" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
                            "friends" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
                            _ => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
                        };
                        break;
                    case "--delete": _deleteId = ulong.Parse(args[++i]); break;
                }
            }

            if (_deleteId == 0ul && (string.IsNullOrEmpty(_contentDir) || !Directory.Exists(_contentDir)))
            {
                Console.Error.WriteLine("错误: --content 目录不存在");
                return 1;
            }

            if (!File.Exists("steam_appid.txt"))
            {
                Console.WriteLine("提示: 未找到 steam_appid.txt（将使用 Steam 当前上下文的 AppID）");
            }
            if (!File.Exists("steam_api64.dll"))
            {
                Console.Error.WriteLine("错误: 缺少 steam_api64.dll（请从游戏 Plugins 目录复制）");
                return 1;
            }

            if (!SteamAPI.Init())
            {
                Console.Error.WriteLine("SteamAPI.Init 失败 — Steam 客户端是否在运行并已登录？");
                return 2;
            }
            Console.WriteLine("Steam 初始化成功, AppID = " + SteamUtils.GetAppID());

            if (_deleteId != 0ul)
            {
                Console.WriteLine("删除物品 " + _deleteId + " ...");
                var delCall = CallResult<DeleteItemResult_t>.Create(OnDeleteResult);
                delCall.Set(SteamUGC.DeleteItem(new PublishedFileId_t(_deleteId)));
                var delDeadline = DateTime.UtcNow.AddMinutes(3);
                while (!Done.IsSet && DateTime.UtcNow < delDeadline)
                {
                    SteamAPI.RunCallbacks();
                    Thread.Sleep(50);
                }
                SteamAPI.Shutdown();
                if (!_ok) { Console.Error.WriteLine("删除未成功（超时或被拒）。"); return 3; }
                Console.WriteLine("物品 " + _deleteId + " 已删除。");
                return 0;
            }

            if (_itemId.m_PublishedFileId == 0ul)
            {
                Console.WriteLine("创建新工坊物品...");
                _createCall = CallResult<CreateItemResult_t>.Create(OnCreateResult);
                _createCall.Set(SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeCommunity));
            }
            else
            {
                Console.WriteLine("更新已有物品 " + _itemId.m_PublishedFileId);
                PrepareAndSubmit();
            }

            // 泵回调直到完成（上限 15 分钟）
            var deadline = DateTime.UtcNow.AddMinutes(15);
            var lastLog = DateTime.MinValue;
            while (!Done.IsSet && DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();
                if (_handle.m_UGCUpdateHandle != 0ul && (DateTime.UtcNow - lastLog).TotalSeconds >= 3)
                {
                    var st = SteamUGC.GetItemUpdateProgress(_handle, out var p, out var t);
                    if (st == EItemUpdateStatus.k_EItemUpdateStatusUploadingContent && t > 0)
                    {
                        lastLog = DateTime.UtcNow;
                        Console.WriteLine($"上传进度: {(p / t * 100d):F1}%");
                    }
                }
                Thread.Sleep(50);
            }

            SteamAPI.Shutdown();
            if (!_ok)
            {
                Console.Error.WriteLine("上传未成功完成（超时或失败）。");
                return 3;
            }
            Console.WriteLine("完成! PublishedFileId = " + _itemId.m_PublishedFileId);
            Console.WriteLine("物品页: https://steamcommunity.com/sharedfiles/filedetails/?id=" + _itemId.m_PublishedFileId);
            return 0;
        }

        private static void OnCreateResult(CreateItemResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK)
            {
                Console.Error.WriteLine("CreateItem 失败: " + r.m_eResult);
                Done.Set();
                return;
            }
            _itemId = r.m_nPublishedFileId;
            Console.WriteLine("物品已创建: " + _itemId.m_PublishedFileId);
            PrepareAndSubmit();
        }

        private static void PrepareAndSubmit()
        {
            _handle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), _itemId);
            bool ok = true;
            ok &= SteamUGC.SetItemTitle(_handle, _title);
            ok &= SteamUGC.SetItemDescription(_handle, _desc);
            ok &= SteamUGC.SetItemVisibility(_handle, _visibility);
            ok &= SteamUGC.SetItemContent(_handle, Path.GetFullPath(_contentDir));
            if (!string.IsNullOrEmpty(_previewFile) && File.Exists(_previewFile))
            {
                ok &= SteamUGC.SetItemPreview(_handle, Path.GetFullPath(_previewFile));
            }
            if (!ok)
            {
                Console.Error.WriteLine("SetItem* 调用失败");
                Done.Set();
                return;
            }
            _submitCall = CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitResult);
            _submitCall.Set(SteamUGC.SubmitItemUpdate(_handle, _changenote));
            Console.WriteLine("已提交，等待 Steam 回调...");
        }

        private static void OnSubmitResult(SubmitItemUpdateResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK)
            {
                Console.Error.WriteLine("SubmitItemUpdate 失败: " + r.m_eResult);
                Done.Set();
                return;
            }
            Console.WriteLine("Steam 确认上传成功。");
            _ok = true;
            Done.Set();
        }

        private static void OnDeleteResult(DeleteItemResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK)
            {
                Console.Error.WriteLine("DeleteItem 失败: " + r.m_eResult);
                Done.Set();
                return;
            }
            Console.WriteLine("Steam 确认删除成功。");
            _ok = true;
            Done.Set();
        }
    }
}
