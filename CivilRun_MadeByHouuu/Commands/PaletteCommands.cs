using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    public class PaletteCommands
    {
        /// <summary>
        /// CivilRun_MadeByHouuu 팔레트 열기/닫기 토글 (명령어: DP)
        /// </summary>
        [CommandMethod("DP")]
        public void TogglePalette()
        {
            var palette = CivilRun_MadeByHouuuApp.GetOrCreatePalette();
            if (palette.Visibility == System.Windows.Visibility.Visible)
                palette.Hide();
            else
                palette.Show();
        }
    }
}
