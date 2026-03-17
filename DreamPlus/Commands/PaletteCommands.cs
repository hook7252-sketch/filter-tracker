using Autodesk.AutoCAD.Runtime;

namespace DreamPlus.Commands
{
    public class PaletteCommands
    {
        /// <summary>
        /// DreamPlus 팔레트 열기/닫기 토글 (명령어: DP)
        /// </summary>
        [CommandMethod("DP")]
        public void TogglePalette()
        {
            var palette = DreamPlusApp.GetOrCreatePalette();
            if (palette.Visibility == System.Windows.Visibility.Visible)
                palette.Hide();
            else
                palette.Show();
        }
    }
}
