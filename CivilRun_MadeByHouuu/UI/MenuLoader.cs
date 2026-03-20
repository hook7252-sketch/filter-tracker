using System;
using Autodesk.AutoCAD.ApplicationServices;

namespace CivilRun_MadeByHouuu.UI
{
    internal static class MenuLoader
    {
        private const string MENU_NAME = "CivilRun";

        public static void CreateMenu()
        {
            try
            {
                var acadGroup = Application.MenuGroups["ACAD"];

                // 이미 등록됐으면 건너뜀
                foreach (PopupMenu existing in acadGroup.Menus)
                    if (existing.Name.Equals(MENU_NAME, StringComparison.OrdinalIgnoreCase))
                        return;

                var root = acadGroup.Menus.Add(MENU_NAME);

                // ─ 좌표/측량
                var coord = root.Items.AddSubMenu(root.Items.Count, "좌표/측량(&C)");
                Add(coord, "좌표 삽입",            "DP_COORD");
                Add(coord, "좌표 내보내기 (CSV)",   "DP_COORDEXP");
                Add(coord, "좌표 가져오기 (CSV)",   "DP_COORDIMP");
                Sep(coord);
                Add(coord, "거리/방위각 계산",      "DP_DISTAZ");
                Add(coord, "방위각으로 선 그리기",  "DP_AZLINE");
                Add(coord, "세그먼트 길이 표시",    "DP_SEGLEN");
                Add(coord, "총 길이 계산",          "DP_PLLEN");
                Add(coord, "교점 마킹",             "DP_IPMARK");

                // ─ 면적/토량
                var area = root.Items.AddSubMenu(root.Items.Count, "면적/토량(&A)");
                Add(area, "면적 표시",              "DP_AREATEXT");
                Add(area, "면적 합산",              "DP_AREASUM");
                Add(area, "토량 계산 (양단면/각주)", "DP_EARTHWORK");

                // ─ 종단/횡단/도로
                var prof = root.Items.AddSubMenu(root.Items.Count, "종단/횡단/도로(&P)");
                Add(prof, "종단 정보 추출",          "DP_PROFILE");
                Add(prof, "횡단선 그리기",           "DP_CROSS");
                Add(prof, "종단 곡선 (포물선)",      "DP_VCURVE");
                Add(prof, "클로소이드 그리기",       "DP_CLOTHOID");
                Sep(prof);
                Add(prof, "등고선 표고 설정",        "DP_ELEVSET");
                Add(prof, "점 표고 확인",            "DP_ELPOINT");

                // ─ 레이어
                var lay = root.Items.AddSubMenu(root.Items.Count, "레이어(&L)");
                Add(lay, "레이어 생성",              "DP_LAYER_NEW");
                Add(lay, "레이어 목록",              "DP_LAYER_LIST");
                Sep(lay);
                Add(lay, "레이어 끄기",              "DP_LOF");
                Add(lay, "레이어 켜기 (전체)",       "DP_LON");
                Add(lay, "레이어 분리",              "DP_LISO");
                Add(lay, "레이어 분리 해제",         "DP_LUISO");
                Sep(lay);
                Add(lay, "레이어 잠금",              "DP_LLK");
                Add(lay, "레이어 잠금 해제 (전체)",  "DP_LULK");
                Sep(lay);
                Add(lay, "현재 레이어로 변경",       "DP_LCUR");
                Add(lay, "선택 → 현재 레이어",      "DP_LCC");
                Add(lay, "다른 레이어로 이동",       "DP_MEO");
                Add(lay, "레이어 병합",              "DP_LME");
                Add(lay, "빈 레이어 삭제",           "DP_LAYER_DELETE_EMPTY");
                Sep(lay);
                Add(lay, "레이어 상태 저장",         "DP_LSAVE");
                Add(lay, "레이어 상태 복원",         "DP_LRESTORE");
                Add(lay, "레이어 색상 변경",         "DP_LAYER_COLOR");

                // ─ 문자/치수
                var txt = root.Items.AddSubMenu(root.Items.Count, "문자/치수(&T)");
                Add(txt, "찾기/바꾸기",              "DP_TFR");
                Add(txt, "문자 높이 변경",           "DP_TEXT_HEIGHT");
                Add(txt, "문자 회전",                "DP_TROT");
                Add(txt, "문자 정렬 (X/Y)",          "DP_TALIGN");
                Add(txt, "문자 박스",                "DP_TBOX");
                Sep(txt);
                Add(txt, "순번 매기기",              "DP_TNUM");
                Add(txt, "접두/접미어 추가",         "DP_TPREFIX");
                Add(txt, "숫자 증감",                "DP_TINC");
                Add(txt, "대소문자 변환",            "DP_TCASE");
                Sep(txt);
                Add(txt, "치수 축척 변경",           "DP_DIM_SCALE");
                Add(txt, "치수 스타일 변경",         "DP_DIM_STYLE");
                Add(txt, "치수 레이어 이동",         "DP_DIM_LAYER");

                // ─ 출력/내보내기
                var exp = root.Items.AddSubMenu(root.Items.Count, "출력/내보내기(&E)");
                Add(exp, "PDF 출력",                 "DP_PLOT_PDF");
                Add(exp, "DXF 내보내기",             "DP_EXPORT_DXF");
                Sep(exp);
                Add(exp, "레이아웃 생성",            "DP_LAYOUT_NEW");
                Add(exp, "레이아웃 복사",            "DP_LAYOUT_COPY");
                Add(exp, "레이아웃 목록",            "DP_LAYOUT_LIST");

                // 메뉴바 맨 끝에 삽입
                root.InsertInMenuBar(Application.MenuBar.Count);

                Application.DocumentManager.MdiActiveDocument?
                    .Editor.WriteMessage("\n[CivilRun] 메뉴가 메뉴바에 추가되었습니다.\n");
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?
                    .Editor.WriteMessage($"\n[CivilRun] 메뉴 생성 실패: {ex.Message}\n");
            }
        }

        private static void Add(PopupMenu menu, string label, string cmd) =>
            menu.Items.AddMenuItem(menu.Items.Count, label, $"^C^C{cmd}\n");

        private static void Sep(PopupMenu menu) =>
            menu.Items.AddSeparator(menu.Items.Count);
    }
}
