using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CivilRun_MadeByHouuu.UI;

[assembly: ExtensionApplication(typeof(CivilRun_MadeByHouuu.CivilRun_MadeByHouuuApp))]

namespace CivilRun_MadeByHouuu
{
    /// <summary>
    /// CivilRun_MadeByHouuu AutoCAD 플러그인 진입점.
    /// AutoCAD가 DLL을 로드할 때 Initialize(), 언로드 시 Terminate() 호출.
    /// </summary>
    public class CivilRun_MadeByHouuuApp : IExtensionApplication
    {
        private static CivilRun_MadeByHouuuPalette? _palette;

        public void Initialize()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n[CivilRun_MadeByHouuu] 플러그인이 로드되었습니다. 'DP' 명령어로 팔레트를 열 수 있습니다.\n");
        }

        public void Terminate()
        {
            _palette?.Close();
        }

        internal static CivilRun_MadeByHouuuPalette GetOrCreatePalette()
        {
            if (_palette == null || !_palette.IsLoaded)
                _palette = new CivilRun_MadeByHouuuPalette();
            return _palette;
        }
    }
}
