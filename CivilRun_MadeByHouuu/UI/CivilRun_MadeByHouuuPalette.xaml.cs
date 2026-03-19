using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;

namespace CivilRun_MadeByHouuu.UI
{
    public partial class CivilRun_MadeByHouuuPalette : Window
    {
        public CivilRun_MadeByHouuuPalette()
        {
            InitializeComponent();
        }

        /// <summary>버튼의 Tag에 저장된 AutoCAD 명령어를 실행</summary>
        private void OnCommandClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string command)
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                // 팔레트를 숨긴 뒤 AutoCAD 명령 실행 (명령 중 포커스 충돌 방지)
                Hide();
                doc.SendStringToExecute($"{command}\n", true, false, false);
                Show();
            }
        }

        // 닫기 버튼 클릭 시 숨김만 처리 (dispose 방지)
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
