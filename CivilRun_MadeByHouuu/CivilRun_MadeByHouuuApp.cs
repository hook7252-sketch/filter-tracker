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
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n[CivilRun] 플러그인 로드 완료. 상단 리본에서 'CivilRun' 탭을 확인하세요.\n");

            // 리본이 이미 준비된 경우 즉시 등록, 아니면 이벤트 대기
            if (ComponentManager.Ribbon != null)
                RibbonLoader.CreateRibbon();
            else
                ComponentManager.ItemInitialized += OnRibbonReady;
        }

        private static void OnRibbonReady(object sender, ComponentManager.ItemInitializedEventArgs e)
        {
            if (ComponentManager.Ribbon != null)
            {
                RibbonLoader.CreateRibbon();
                ComponentManager.ItemInitialized -= OnRibbonReady;
            }
        }

        public void Terminate() { }
    }
}
