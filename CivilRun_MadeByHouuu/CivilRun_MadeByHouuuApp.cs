using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CivilRun_MadeByHouuu.UI;

[assembly: ExtensionApplication(typeof(CivilRun_MadeByHouuu.CivilRun_MadeByHouuuApp))]

namespace CivilRun_MadeByHouuu
{
    public class CivilRun_MadeByHouuuApp : IExtensionApplication
    {
        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument?
                .Editor.WriteMessage("\n[CivilRun] 로드 완료. 메뉴바에서 'CivilRun' 메뉴를 확인하세요.\n");

            // AutoCAD가 완전히 준비된 후 메뉴 등록
            Application.Idle += OnIdle;
        }

        private static void OnIdle(object sender, System.EventArgs e)
        {
            Application.Idle -= OnIdle;
            MenuLoader.CreateMenu();
        }

        public void Terminate() { }
    }

    /// <summary>수동으로 메뉴를 다시 로드 (DP_MENU)</summary>
    public class MenuCommands
    {
        [CommandMethod("DP_MENU")]
        public void ReloadMenu()
        {
            MenuLoader.CreateMenu();
        }
    }
}
