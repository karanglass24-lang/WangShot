using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Greenshot.Base.Core;
using Greenshot.Base.Interfaces.Plugin;
using Greenshot.Base.Languages;
using Greenshot.Base.Threading;
using Greenshot.Editor.Drawing;
using Greenshot.Editor.Drawing.Fields;
using Greenshot.Editor.Helpers;
using static Greenshot.Editor.Drawing.FilterContainer;

namespace Greenshot.Editor.Forms
{
    public partial class ImageEditorForm
    {
        private const string SensitiveTitle = "민감정보 자동 가리기";

        private bool _sensitiveRunning;

        private void ObfuscateSensitiveInfo()
        {
            // 왕캡: a second click while the text is still recognized would cover everything twice
            if (_sensitiveRunning)
            {
                ShowRibbonStatus(SensitiveTitle + ": 아직 글자를 인식하는 중이에요. 잠시만 기다려 주세요.");
                return;
            }

            AsyncCommand.Run(ObfuscateSensitiveInfoAsync, "Obfuscate sensitive info");
        }

        /// <summary>
        /// 왕샷: OCR of the image, then every phone number, e-mail address, resident registration number and card number is pixelated.
        /// 왕캡: the progress and the result are shown in the status bar (no dialog to close); a failure still gets a dialog.
        /// </summary>
        private async System.Threading.Tasks.Task ObfuscateSensitiveInfoAsync()
        {
            if (_surface == null)
            {
                return;
            }

            var surface = _surface;
            List<IOcrLineFeature> ocrLines;
            _sensitiveRunning = true;
            ShowRibbonStatus(SensitiveTitle + ": 글자를 인식하는 중이에요…");
            Cursor = Cursors.WaitCursor;
            try
            {
                ocrLines = await surface.RecognizeTextAsync();
            }
            catch (Exception ex)
            {
                ShowRibbonStatus(SensitiveTitle + ": " + Texts.Editor.ObfuscateTextOcrFailed, isError: true);
                MessageBox.Show(this, Texts.Editor.ObfuscateTextOcrFailed + ": " + ex.Message, SensitiveTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
                _sensitiveRunning = false;
            }

            if (!ReferenceEquals(surface, _surface) || surface.IsDisposed)
            {
                // The tab was changed or closed meanwhile, its image is not touched
                return;
            }

            if (ocrLines == null)
            {
                ShowRibbonStatus(SensitiveTitle + ": " + Texts.Editor.ObfuscateTextNoOcrProvider, isError: true);
                MessageBox.Show(this, Texts.Editor.ObfuscateTextNoOcrProvider, SensitiveTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<(Dapplo.Windows.Common.Structs.NativeRect Bounds, int TextHeight)> areas;
            try
            {
                areas = SensitiveInfoMatcher.FindAreas(ocrLines);
            }
            catch (Exception ex)
            {
                Log.Warn("The search for personal data failed", ex);
                ShowRibbonStatus(SensitiveTitle + ": 글자를 분석하지 못했어요. 바뀐 곳은 없어요.", isError: true);
                return;
            }

            if (areas.Count == 0)
            {
                ShowRibbonStatus(SensitiveTitle + ": 전화번호·이메일·주민등록번호·카드번호로 보이는 글자를 찾지 못했어요. 바뀐 곳은 없어요.");
                return;
            }

            var containers = new DrawableContainerList();
            foreach (var (bounds, textHeight) in areas)
            {
                var container = new ObfuscateContainer(surface);
                container.SetFieldValue(FieldType.PREPARED_FILTER_OBFUSCATE, PreparedFilter.PIXELIZE);
                ObfuscationStrength.FitToText(container, textHeight, 30, 30);
                // A little room around the text, so the edges of the letters are covered
                int pad = Math.Max(2, bounds.Height / 6);
                container.Left = bounds.Left - pad;
                container.Top = bounds.Top - pad;
                container.Width = bounds.Width + pad * 2;
                container.Height = bounds.Height + pad * 2;
                containers.Add(container);
            }

            surface.AddElements(containers, true);
            ShowRibbonStatus($"{SensitiveTitle}: {areas.Count}곳을 가렸어요. 놓친 곳이 없는지 꼭 확인해 주세요 (Ctrl+Z로 되돌리기).");
        }
    }
}
