using System;
using System.IO;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;

namespace CivilRun_MadeByHouuu.UI
{
    /// <summary>
    /// AutoLISP VLA COM API를 이용해 메뉴바에 CivilRun 풀다운 메뉴를 추가합니다.
    /// Dream 플러그인과 동일한 방식입니다.
    /// </summary>
    internal static class MenuLoader
    {
        public static void CreateMenu()
        {
            try
            {
                var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                var lspPath = Path.Combine(dllDir, "CivilRunMenu.lsp");

                File.WriteAllText(lspPath, BuildLisp(), new UTF8Encoding(false));

                var forwardPath = lspPath.Replace('\\', '/');
                Application.DocumentManager.MdiActiveDocument?
                    .SendStringToExecute($"(load \"{forwardPath}\")\n", true, false, false);
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?
                    .Editor.WriteMessage($"\n[CivilRun] 메뉴 생성 실패: {ex.Message}\n");
            }
        }

        private static string BuildLisp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; CivilRun Menu - auto generated");
            sb.AppendLine("(defun civilrun-menu-create");
            sb.AppendLine("  (/ ac ms root s1 s2 s3 s4 s5 s6 found)");
            sb.AppendLine("  (vl-load-com)");
            sb.AppendLine("  (setq ac (vlax-get-acad-object))");
            sb.AppendLine("  (setq ms (vla-get-menus (vla-item (vla-get-menugroups ac) \"ACAD\")))");
            sb.AppendLine("  (setq found nil)");
            sb.AppendLine("  (vlax-for m ms");
            sb.AppendLine("    (if (= (vla-get-name m) \"CivilRun\") (setq found T)))");
            sb.AppendLine("  (if (not found) (progn");
            sb.AppendLine("    (setq root (vla-add ms \"CivilRun\"))");

            // ─ 좌표/측량  (root → s1)
            Sub(sb, "root", "s1", "좌표/측량");
            Item(sb, "s1", "좌표 삽입",              "DP_COORD");
            Item(sb, "s1", "좌표 내보내기 (CSV)",     "DP_COORDEXP");
            Item(sb, "s1", "좌표 가져오기 (CSV)",     "DP_COORDIMP");
            Sep(sb,  "s1");
            Item(sb, "s1", "XY 좌표 지시선",          "XY");
            Item(sb, "s1", "거리/방위각 계산",        "DP_DISTAZ");
            Item(sb, "s1", "방위각으로 선 그리기",    "DP_AZLINE");
            Item(sb, "s1", "세그먼트 길이 표시",      "DP_SEGLEN");
            Item(sb, "s1", "총 길이 계산",            "DP_PLLEN");
            Item(sb, "s1", "교점 마킹",               "DP_IPMARK");

            // ─ 면적/토량
            Sub(sb, "root", "s2", "면적/토량");
            Item(sb, "s2", "면적 표시",               "DP_AREATEXT");
            Item(sb, "s2", "면적 지시선 (ARL)",        "ARL");
            Item(sb, "s2", "면적 합산",               "DP_AREASUM");
            Item(sb, "s2", "토량 계산 (양단면/각주)", "DP_EARTHWORK");

            // ─ 종단/횡단/도로
            Sub(sb, "root", "s3", "종단/횡단/도로");
            Item(sb, "s3", "종단 정보 추출",          "DP_PROFILE");
            Item(sb, "s3", "횡단선 그리기",           "DP_CROSS");
            Item(sb, "s3", "종단 곡선 (포물선)",      "DP_VCURVE");
            Item(sb, "s3", "클로소이드 그리기",       "DP_CLOTHOID");
            Sep(sb,  "s3");
            Item(sb, "s3", "등고선 표고 설정",        "DP_ELEVSET");
            Item(sb, "s3", "점 표고 확인",            "DP_ELPOINT");

            // ─ 레이어
            Sub(sb, "root", "s4", "레이어");
            Item(sb, "s4", "레이어 생성",             "DP_LAYER_NEW");
            Item(sb, "s4", "레이어 목록",             "DP_LAYER_LIST");
            Sep(sb,  "s4");
            Item(sb, "s4", "레이어 끄기 (기본)",      "DP_LOF");
            Item(sb, "s4", "레이어 켜기 (전체)",      "DP_LON");
            Item(sb, "s4", "레이어 분리",             "DP_LISO");
            Item(sb, "s4", "레이어 분리 해제",        "DP_LUISO");
            Sep(sb,  "s4");
            Item(sb, "s4", "임시 OFF [LOF]",            "LOF");
            Item(sb, "s4", "영구 OFF [LOFF]",           "LOFF");
            Item(sb, "s4", "임시 OFF 복구 [LON]",       "LON");
            Item(sb, "s4", "강제 전체 복구 [LONN]",     "LONN");
            Item(sb, "s4", "선택 레이어만 ON [LOL]",    "LOL");
            Sep(sb,  "s4");
            Item(sb, "s4", "VP 레이어 오버라이드 해제", "LONVP");
            Item(sb, "s4", "전체 레이아웃 레이어 ON",   "LONALL");
            Sep(sb,  "s4");
            Item(sb, "s4", "레이어 잠금",             "DP_LLK");
            Item(sb, "s4", "레이어 잠금 해제 (전체)", "DP_LULK");
            Sep(sb,  "s4");
            Item(sb, "s4", "현재 레이어로 변경",      "DP_LCUR");
            Item(sb, "s4", "선택 -> 현재 레이어",     "DP_LCC");
            Item(sb, "s4", "다른 레이어로 이동",      "DP_MEO");
            Item(sb, "s4", "레이어 병합",             "DP_LME");
            Item(sb, "s4", "빈 레이어 삭제",          "DP_LAYER_DELETE_EMPTY");
            Sep(sb,  "s4");
            Item(sb, "s4", "레이어 상태 저장",        "DP_LSAVE");
            Item(sb, "s4", "레이어 상태 복원",        "DP_LRESTORE");
            Item(sb, "s4", "레이어 색상 변경",        "DP_LAYER_COLOR");

            // ─ 문자/치수
            Sub(sb, "root", "s5", "문자/치수");
            Item(sb, "s5", "찾기/바꾸기",             "DP_TFR");
            Item(sb, "s5", "문자 높이 변경",          "DP_TEXT_HEIGHT");
            Item(sb, "s5", "문자 회전",               "DP_TROT");
            Item(sb, "s5", "문자 정렬 (X/Y)",         "DP_TALIGN");
            Item(sb, "s5", "문자 박스",               "DP_TBOX");
            Sep(sb,  "s5");
            Item(sb, "s5", "순번 매기기",             "DP_TNUM");
            Item(sb, "s5", "접두/접미어 추가",        "DP_TPREFIX");
            Item(sb, "s5", "숫자 증감",               "DP_TINC");
            Item(sb, "s5", "대소문자 변환",           "DP_TCASE");
            Sep(sb,  "s5");
            Item(sb, "s5", "폰트 일괄 변경 (malgun)", "FC");
            Item(sb, "s5", "문자 아래로 복제 (WCS)",  "RB");
            Item(sb, "s5", "문자 위로 복제 (WCS)",    "RU");
            Item(sb, "s5", "문자 아래로 복제 (화면)", "RBS");
            Item(sb, "s5", "문자 위로 복제 (화면)",   "RUS");
            Item(sb, "s5", "문자 아래로 복제 (UCS)",  "RBU");
            Item(sb, "s5", "문자 위로 복제 (UCS)",    "RUU");
            Sep(sb,  "s5");
            Item(sb, "s5", "치수 축척 변경",          "DP_DIM_SCALE");
            Item(sb, "s5", "치수 스타일 변경",        "DP_DIM_STYLE");
            Item(sb, "s5", "치수 레이어 이동",        "DP_DIM_LAYER");

            // ─ 블록/도구
            Sub(sb, "root", "s6b", "블록/도구");
            Item(sb, "s6b", "블록 삽입",               "DP_BLOCK_INSERT");
            Item(sb, "s6b", "블록 목록",               "DP_BLOCK_LIST");
            Item(sb, "s6b", "블록 개수 세기",          "DP_BLOCK_COUNT");
            Item(sb, "s6b", "블록 교체",               "DP_BLOCK_REPLACE");
            Sep(sb,   "s6b");
            Item(sb, "s6b", "원+번호 연속 복제 [CU]",  "CU");
            Item(sb, "s6b", "XY 비균일 스케일 [XYB]",  "XYB");
            Sep(sb,   "s6b");
            Item(sb, "s6b", "변경있음 배지 [BY]",       "BY");
            Item(sb, "s6b", "변경없음 배지 [BN]",       "BN");

            // ─ 출력/내보내기
            Sub(sb, "root", "s6", "출력/내보내기");
            Item(sb, "s6", "PDF 출력",                "DP_PLOT_PDF");
            Item(sb, "s6", "DXF 내보내기",            "DP_EXPORT_DXF");
            Sep(sb,  "s6");
            Item(sb, "s6", "레이아웃 생성",           "DP_LAYOUT_NEW");
            Item(sb, "s6", "레이아웃 복사",           "DP_LAYOUT_COPY");
            Item(sb, "s6", "레이아웃 목록",           "DP_LAYOUT_LIST");

            sb.AppendLine("    (vla-insertinmenubar root (vla-get-count (vla-get-menubar ac)))");
            sb.AppendLine("    (princ \"\\n[CivilRun] 메뉴가 추가되었습니다.\")");
            sb.AppendLine("  ))");
            sb.AppendLine("  (princ)");
            sb.AppendLine(")");
            sb.AppendLine("(civilrun-menu-create)");

            return sb.ToString();
        }

        // parent: 부모 PopupMenu 변수명 (root 또는 상위 submenu)
        // sVar:   새로 만들 submenu 변수명
        private static void Sub(StringBuilder sb, string parent, string sVar, string label) =>
            sb.AppendLine($"    (setq {sVar} (vla-addsubmenu {parent} (vla-get-count {parent}) \"{label}\"))");

        // menu: PopupMenu 변수명에 직접 addmenuitem
        private static void Item(StringBuilder sb, string menu, string label, string cmd) =>
            sb.AppendLine($"    (vla-addmenuitem {menu} (vla-get-count {menu}) \"{label}\" \"(command \\\"{cmd}\\\")\")");

        private static void Sep(StringBuilder sb, string menu) =>
            sb.AppendLine($"    (vla-addseparator {menu} (vla-get-count {menu}))");
    }
}
