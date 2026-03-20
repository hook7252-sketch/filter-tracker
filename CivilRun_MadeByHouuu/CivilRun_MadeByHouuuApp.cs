using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using CivilRun_MadeByHouuu.UI;

[assembly: ExtensionApplication(typeof(CivilRun_MadeByHouuu.CivilRun_MadeByHouuuApp))]

namespace CivilRun_MadeByHouuu
{
    public class CivilRun_MadeByHouuuApp : IExtensionApplication
    {
        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument?
                .Editor.WriteMessage("\n[CivilRun] 로드 완료. 리본 탭이 없으면 명령창에 'DP_RIBBON' 입력\n");

            // Idle 이벤트: AutoCAD가 완전히 준비된 후 리본 등록
            Application.Idle += OnIdle;
        }

        private static void OnIdle(object sender, System.EventArgs e)
        {
            Application.Idle -= OnIdle;

            // 리본이 없으면 RIBBON 명령으로 켜고 다시 Idle에서 시도
            if (ComponentManager.Ribbon == null)
            {
                Application.DocumentManager.MdiActiveDocument?
                    .SendStringToExecute("RIBBON\n", true, false, false);
                Application.Idle += OnIdleRetry;
            }
            else
            {
                RibbonLoader.CreateRibbon();
            }
        }

        private static void OnIdleRetry(object sender, System.EventArgs e)
        {
            Application.Idle -= OnIdleRetry;
            if (ComponentManager.Ribbon != null)
                RibbonLoader.CreateRibbon();
        }

        public void Terminate() { }
    }

    /// <summary>명령창에서 수동으로 CivilRun 리본 탭을 로드</summary>
    public class RibbonCommands
    {
        [CommandMethod("DP_RIBBON")]
        public void LoadRibbon()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (ComponentManager.Ribbon == null)
            {
                doc?.SendStringToExecute("RIBBON\n", true, false, false);
                doc?.Editor.WriteMessage("\n리본을 켰습니다. 잠시 후 다시 'DP_RIBBON' 을 입력하세요.\n");
                return;
            }
            RibbonLoader.CreateRibbon();
            doc?.Editor.WriteMessage("\n[CivilRun] 리본 탭을 로드했습니다.\n");
        }
    }
}
