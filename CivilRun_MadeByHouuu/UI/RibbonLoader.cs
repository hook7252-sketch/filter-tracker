using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;

namespace CivilRun_MadeByHouuu.UI
{
    /// <summary>
    /// AutoCAD 리본에 "CivilRun" 탭과 패널/버튼을 등록합니다.
    /// </summary>
    internal static class RibbonLoader
    {
        private const string TAB_ID = "CivilRun_Tab";

        public static void CreateRibbon()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            // 이미 탭이 있으면 재등록하지 않음
            if (ribbon.FindTab(TAB_ID) != null) return;

            var tab = new RibbonTab
            {
                Title = "CivilRun",
                Id = TAB_ID,
                IsVisible = true
            };

            tab.Panels.Add(MakePanel_Coord());
            tab.Panels.Add(MakePanel_Area());
            tab.Panels.Add(MakePanel_Profile());
            tab.Panels.Add(MakePanel_Layer());
            tab.Panels.Add(MakePanel_Text());
            tab.Panels.Add(MakePanel_Export());

            ribbon.Tabs.Add(tab);
            ribbon.ActiveTab = tab;
        }

        // ── 헬퍼: 버튼 생성 ─────────────────────────────────
        private static RibbonButton Btn(string label, string command, string tooltip = "")
        {
            var btn = new RibbonButton
            {
                Text = label,
                CommandParameter = command + "\n",
                CommandHandler = new RelayCommand(cmd =>
                {
                    var doc = Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute((string)cmd, true, false, false);
                }),
                ShowText = true,
                Size = RibbonItemSize.Standard,
                ToolTip = string.IsNullOrEmpty(tooltip) ? label : tooltip
            };
            return btn;
        }

        private static RibbonPanel MakePanel(string title, params RibbonItem[] items)
        {
            var src = new RibbonPanelSource { Title = title };
            var row = new RibbonRowPanel();
            bool firstInRow = true;
            foreach (var item in items)
            {
                if (!firstInRow) row.Items.Add(new RibbonRowBreak());
                row.Items.Add(item);
                firstInRow = false;
            }
            src.Items.Add(row);
            return new RibbonPanel { Source = src };
        }

        // ── 패널 정의 ────────────────────────────────────────

        private static RibbonPanel MakePanel_Coord() => MakePanel("좌표/측량",
            Btn("좌표삽입",    "DP_COORD",    "클릭 위치에 좌표 문자 삽입"),
            Btn("좌표내보내기", "DP_COORDEXP", "선택 객체 좌표를 CSV로 저장"),
            Btn("좌표가져오기", "DP_COORDIMP", "CSV 좌표로 3D폴리라인 그리기"),
            Btn("거리/방위각",  "DP_DISTAZ",   "두 점 사이 거리·방위각 계산"),
            Btn("방위각선",    "DP_AZLINE",   "방위각+거리로 선 그리기"),
            Btn("세그먼트길이", "DP_SEGLEN",   "폴리라인 각 구간 길이 표시"),
            Btn("총길이계산",   "DP_PLLEN",    "선택 객체 총 길이 계산"),
            Btn("교점마킹",    "DP_IPMARK",   "선들의 교점에 X 마커 표시")
        );

        private static RibbonPanel MakePanel_Area() => MakePanel("면적/토량",
            Btn("면적표시",  "DP_AREATEXT",  "폴리라인/해치 면적 계산 후 삽입"),
            Btn("면적합산",  "DP_AREASUM",   "선택 객체 면적 합계"),
            Btn("토량계산",  "DP_EARTHWORK", "양단면 평균·각주공식 토량 계산")
        );

        private static RibbonPanel MakePanel_Profile() => MakePanel("종단/횡단/도로",
            Btn("종단추출",   "DP_PROFILE",    "폴리라인에서 종단 정보 CSV 추출"),
            Btn("횡단선",     "DP_CROSS",      "법선 방향으로 횡단선 그리기"),
            Btn("종단곡선",   "DP_VCURVE",     "VPI·경사로 종단 포물선 그리기"),
            Btn("클로소이드", "DP_CLOTHOID",   "완화곡선(클로소이드) 그리기"),
            Btn("표고설정",   "DP_ELEVSET",    "등고선 폴리라인 Z값 일괄 설정"),
            Btn("점표고확인", "DP_ELPOINT",    "클릭 점의 표고 확인")
        );

        private static RibbonPanel MakePanel_Layer() => MakePanel("레이어",
            Btn("레이어생성",   "DP_LAYER_NEW",          "레이어 이름으로 새 레이어 생성"),
            Btn("레이어목록",   "DP_LAYER_LIST",         "전체 레이어 목록 출력"),
            Btn("레이어끄기",   "DP_LOF",                "선택 객체 레이어 끄기"),
            Btn("레이어켜기",   "DP_LON",                "모든 레이어 켜기"),
            Btn("레이어분리",   "DP_LISO",               "선택 레이어만 켜고 나머지 끄기"),
            Btn("분리해제",     "DP_LUISO",              "레이어 분리 해제(모두 복원)"),
            Btn("레이어잠금",   "DP_LLK",                "선택 객체 레이어 잠금"),
            Btn("잠금해제",     "DP_LULK",               "모든 레이어 잠금 해제"),
            Btn("현재레이어",   "DP_LCUR",               "선택 객체의 레이어를 현재로"),
            Btn("레이어변경",   "DP_LCC",                "선택 객체를 현재 레이어로 변경"),
            Btn("레이어이동",   "DP_MEO",                "선택 객체를 다른 레이어로 이동"),
            Btn("레이어병합",   "DP_LME",                "여러 레이어를 하나로 합치기"),
            Btn("빈레이어삭제", "DP_LAYER_DELETE_EMPTY", "객체 없는 레이어 삭제"),
            Btn("레이어저장",   "DP_LSAVE",              "현재 레이어 상태 저장"),
            Btn("레이어복원",   "DP_LRESTORE",           "저장된 레이어 상태 복원")
        );

        private static RibbonPanel MakePanel_Text() => MakePanel("문자/치수",
            Btn("찾기/바꾸기",  "DP_TFR",        "문자 찾아 바꾸기"),
            Btn("문자높이",     "DP_TEXT_HEIGHT", "선택 텍스트 높이 변경"),
            Btn("문자회전",     "DP_TROT",        "선택 텍스트 회전 각도 변경"),
            Btn("문자정렬",     "DP_TALIGN",      "텍스트 X/Y축 기준 정렬"),
            Btn("문자박스",     "DP_TBOX",        "텍스트 주위에 사각형 그리기"),
            Btn("번호매기기",   "DP_TNUM",        "선택 텍스트에 순번 부여"),
            Btn("접두/접미",    "DP_TPREFIX",     "텍스트에 접두어·접미어 추가"),
            Btn("숫자증감",     "DP_TINC",        "텍스트 내 숫자를 일정값 증감"),
            Btn("대소문자",     "DP_TCASE",       "텍스트 대소문자 변환"),
            Btn("치수축척",     "DP_DIM_SCALE",   "치수 전체 축척 변경"),
            Btn("치수스타일",   "DP_DIM_STYLE",   "치수 스타일 변경"),
            Btn("치수레이어",   "DP_DIM_LAYER",   "치수를 지정 레이어로 이동")
        );

        private static RibbonPanel MakePanel_Export() => MakePanel("출력/내보내기",
            Btn("PDF출력",      "DP_PLOT_PDF",    "현재 레이아웃을 PDF로 출력"),
            Btn("DXF내보내기",  "DP_EXPORT_DXF",  "현재 도면을 DXF로 저장"),
            Btn("레이아웃생성", "DP_LAYOUT_NEW",  "새 레이아웃 추가"),
            Btn("레이아웃복사", "DP_LAYOUT_COPY", "레이아웃 복사"),
            Btn("레이아웃목록", "DP_LAYOUT_LIST", "레이아웃 목록 출력")
        );
    }

    /// <summary>리본 버튼 커맨드 핸들러</summary>
    internal class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly System.Action<object> _execute;
        public RelayCommand(System.Action<object> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter!);
        public event System.EventHandler? CanExecuteChanged;
    }
}
