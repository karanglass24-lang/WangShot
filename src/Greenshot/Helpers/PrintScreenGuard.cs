using System;
using System.Diagnostics;
using System.Windows.Forms;
using Dapplo.Ini;
using Greenshot.Base.Core;
using log4net;
using Microsoft.Win32;

namespace Greenshot.Helpers
{
    /// <summary>
    /// Windows 11 opens its own snipping tool on PrintScreen ("Use the Print screen button to open screen capture").
    /// That competes with the PrintScreen hotkey of 왕샷. At the start this asks once whether to turn the Windows setting off.
    /// </summary>
    public static class PrintScreenGuard
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(PrintScreenGuard));
        private const string KeyboardKey = @"Control Panel\Keyboard";
        private const string SnippingValue = "PrintScreenKeyForSnippingEnabled";
        private const string AppKey = @"Software\WangShot";
        private const string DontAskValue = "DontAskPrintScreen";

        /// <summary>
        /// True when Windows opens the snipping tool on PrintScreen
        /// </summary>
        public static bool IsWindowsSnippingOnPrintScreen()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyboardKey);
                return key?.GetValue(SnippingValue) is int value && value == 1;
            }
            catch (Exception ex)
            {
                Log.Debug("Couldn't read the PrintScreen setting of Windows", ex);
                return false;
            }
        }

        /// <summary>
        /// Turn the PrintScreen snipping of Windows off (the setting of the current user only)
        /// </summary>
        public static bool DisableWindowsSnippingOnPrintScreen()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KeyboardKey, true);
                key?.SetValue(SnippingValue, 0, RegistryValueKind.DWord);
                return !IsWindowsSnippingOnPrintScreen();
            }
            catch (Exception ex)
            {
                Log.Warn("Couldn't change the PrintScreen setting of Windows", ex);
                return false;
            }
        }

        private static bool UsesPrintScreen()
        {
            var conf = IniConfigRegistry.GetSection<ICoreConfiguration>();
            return Contains(conf.RegionHotkey) || Contains(conf.FullscreenHotkey) || Contains(conf.WindowHotkey) || Contains(conf.LastregionHotkey);
        }

        private static bool Contains(string hotkey) => hotkey != null && hotkey.IndexOf("PrintScreen", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool DontAsk()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(AppKey);
                return key?.GetValue(DontAskValue) is int value && value == 1;
            }
            catch
            {
                return false;
            }
        }

        private static void RememberDontAsk()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(AppKey);
                key?.SetValue(DontAskValue, 1, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Log.Debug("Couldn't remember the answer", ex);
            }
        }

        /// <summary>
        /// Ask whether Windows may stop using PrintScreen, only when the setting is on and a hotkey of 왕샷 uses PrintScreen
        /// </summary>
        public static void CheckAndAsk()
        {
            if (!UsesPrintScreen() || !IsWindowsSnippingOnPrintScreen() || DontAsk())
            {
                return;
            }

            var answer = MessageBox.Show(
                "Windows가 PrintScreen 키를 '캡처 도구(스니핑 도구)'에 사용하고 있어서 왕샷 단축키와 충돌할 수 있어요.\n\n" +
                "Windows의 해당 설정을 끌까요? (현재 사용자 설정만 바뀌고, 설정 > 접근성 > 키보드에서 다시 켤 수 있어요)\n\n" +
                "예: 설정 끄기\n아니요: 이번엔 그대로 두기\n취소: 다시 묻지 않기",
                "왕샷 - PrintScreen 충돌 방지",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            switch (answer)
            {
                case DialogResult.Yes:
                    if (DisableWindowsSnippingOnPrintScreen())
                    {
                        MessageBox.Show("설정을 껐어요. 이미 열려 있는 프로그램은 다시 로그인하거나 재부팅해야 적용될 수 있어요.", "왕샷", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo("ms-settings:easeofaccess-keyboard") { UseShellExecute = true });
                    }
                    break;
                case DialogResult.Cancel:
                    RememberDontAsk();
                    break;
            }
        }
    }
}
