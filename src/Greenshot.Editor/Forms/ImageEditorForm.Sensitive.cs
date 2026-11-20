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

        private void ObfuscateSensitiveInfo()
        {
            AsyncCommand.Run(ObfuscateSensitiveInfoAsync, "Obfuscate sensitive info");
        }

        /// <summary>
        /// 왕샷: OCR of the image, then every phone number, e-mail address, resident registration number and card number is pixelated
        /// </summary>
        private async System.Threading.Tasks.Task ObfuscateSensitiveInfoAsync()
        {
            if (_surface == null)
            {
                return;
            }

            List<IOcrLineFeature> ocrLines;
            Cursor = Cursors.WaitCursor;
            try
            {
                ocrLines = await _surface.RecognizeTextAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Texts.Editor.ObfuscateTextOcrFailed + ": " + ex.Message, SensitiveTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (ocrLines == null)
            {
                MessageBox.Show(Texts.Editor.ObfuscateTextNoOcrProvider, SensitiveTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var areas = SensitiveInfoMatcher.FindAreas(ocrLines);
            if (areas.Count == 0)
            {
                MessageBox.Show("전화번호, 이메일, 주민등록번호, 카드번호로 보이는 글자를 찾지 못했어요.", SensitiveTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var containers = new DrawableContainerList();
            foreach (var (bounds, textHeight) in areas)
            {
                var container = new ObfuscateContainer(_surface);
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

            _surface.AddElements(containers, true);
            MessageBox.Show($"{areas.Count}곳을 가렸어요. 놓친 곳이 없는지 꼭 확인해 주세요.", SensitiveTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
