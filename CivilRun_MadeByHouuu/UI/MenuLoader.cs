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
            sb.AppendLine("  (/ ac ms root ri s1 s2 s3 s4 s5 s6 i1 i2 i3 i4 i5 i6 found)");
            sb.AppendLine("  (vl-load-com)");
            sb.AppendLine("  (setq ac (vlax-get-acad-object))");
            sb.AppendLine("  (setq ms (vla-get-menus (vla-item (vla-get-menugroups ac) \"ACAD\")))");
            sb.AppendLine("  (setq found nil)");
            sb.AppendLine("  (vlax-for m ms");
            sb.AppendLine("    (if (= (vla-get-name m) \"CivilRun\") (setq found T)))");
            sb.AppendLine("  (if (not found) (progn");
            sb.AppendLine("    (setq root (vla-add ms \"CivilRun\"))");
            sb.AppendLine("    (setq ri (vla-get-menuitems root))");

            // ─ 좌표/측량
            Sub(sb, "s1", "i1", "좌표/측량");
            Item(sb, "i1", "좌표 삽입",              "DP_COORD");
            Item(sb, "i1", "좌표 내보내기 (CSV)",     "DP_COORDEXP");
            Item(sb, "i1", "좌표 가져오기 (CSV)",     "DP_COORDIMP");
            Sep(sb,  "i1");
            Item(sb, "i1", "거리/방위각 계산",        "DP_DISTAZ");
            Item(sb, "i1", "방위각으로 선 그리기",    "DP_AZLINE");
            Item(sb, "i1", "세그먼트 길이 표시",      "DP_SEGLEN");
            Item(sb, "i1", "총 길이 계산",            "DP_PLLEN");
            Item(sb, "i1", "교점 마킹",               "DP_IPMARK");

            // ─ 면적/토량
            Sub(sb, "s2", "i2", "면적/토량");
            Item(sb, "i2", "면적 표시",               "DP_AREATEXT");
            Item(sb, "i2", "면적 합산",               "DP_AREASUM");
            Item(sb, "i2", "토량 계산 (양단면/각주)", "DP_EARTHWORK");

            // ─ 종단/횡단/도로
            Sub(sb, "s3", "i3", "종단/횡단/도로");
            Item(sb, "i3", "종단 정보 추출",          "DP_PROFILE");
            Item(sb, "i3", "횡단선 그리기",           "DP_CROSS");
            Item(sb, "i3", "종단 곡선 (포물선)",      "DP_VCURVE");
            Item(sb, "i3", "클로소이드 그리기",       "DP_CLOTHOID");
            Sep(sb,  "i3");
            Item(sb, "i3", "등고선 표고 설정",        "DP_ELEVSET");
            Item(sb, "i3", "점 표고 확인",            "DP_ELPOINT");

            // ─ 레이어
            Sub(sb, "s4", "i4", "레이어");
            Item(sb, "i4", "레이어 생성",             "DP_LAYER_NEW");
            Item(sb, "i4", "레이어 목록",             "DP_LAYER_LIST");
            Sep(sb,  "i4");
            Item(sb, "i4", "레이어 끄기",             "DP_LOF");
            Item(sb, "i4", "레이어 켜기 (전체)",      "DP_LON");
            Item(sb, "i4", "레이어 분리",             "DP_LISO");
            Item(sb, "i4", "레이어 분리 해제",        "DP_LUISO");
            Sep(sb,  "i4");
            Item(sb, "i4", "레이어 잠금",             "DP_LLK");
            Item(sb, "i4", "레이어 잠금 해제 (전체)", "DP_LULK");
            Sep(sb,  "i4");
            Item(sb, "i4", "현재 레이어로 변경",      "DP_LCUR");
            Item(sb, "i4", "선택 -> 현재 레이어",    "DP_LCC");
            Item(sb, "i4", "다른 레이어로 이동",      "DP_MEO");
            Item(sb, "i4", "레이어 병합",             "DP_LME");
            Item(sb, "i4", "빈 레이어 삭제",          "DP_LAYER_DELETE_EMPTY");
            Sep(sb,  "i4");
            Item(sb, "i4", "레이어 상태 저장",        "DP_LSAVE");
            Item(sb, "i4", "레이어 상태 복원",        "DP_LRESTORE");
            Item(sb, "i4", "레이어 색상 변경",        "DP_LAYER_COLOR");

            // ─ 문자/치수
            Sub(sb, "s5", "i5", "문자/치수");
            Item(sb, "i5", "찾기/바꾸기",             "DP_TFR");
            Item(sb, "i5", "문자 높이 변경",          "DP_TEXT_HEIGHT");
            Item(sb, "i5", "문자 회전",               "DP_TROT");
            Item(sb, "i5", "문자 정렬 (X/Y)",         "DP_TALIGN");
            Item(sb, "i5", "문자 박스",               "DP_TBOX");
            Sep(sb,  "i5");
            Item(sb, "i5", "순번 매기기",             "DP_TNUM");
            Item(sb, "i5", "접두/접미어 추가",        "DP_TPREFIX");
            Item(sb, "i5", "숫자 증감",               "DP_TINC");
            Item(sb, "i5", "대소문자 변환",           "DP_TCASE");
            Sep(sb,  "i5");
            Item(sb, "i5", "치수 축척 변경",          "DP_DIM_SCALE");
            Item(sb, "i5", "치수 스타일 변경",        "DP_DIM_STYLE");
            Item(sb, "i5", "치수 레이어 이동",        "DP_DIM_LAYER");

            // ─ 출력/내보내기
            Sub(sb, "s6", "i6", "출력/내보내기");
            Item(sb, "i6", "PDF 출력",                "DP_PLOT_PDF");
            Item(sb, "i6", "DXF 내보내기",            "DP_EXPORT_DXF");
            Sep(sb,  "i6");
            Item(sb, "i6", "레이아웃 생성",           "DP_LAYOUT_NEW");
            Item(sb, "i6", "레이아웃 복사",           "DP_LAYOUT_COPY");
            Item(sb, "i6", "레이아웃 목록",           "DP_LAYOUT_LIST");

            sb.AppendLine("    (vla-insertinmenubar root (1+ (vla-get-count (vla-get-menubar ac))))");
            sb.AppendLine("    (princ \"\\n[CivilRun] 메뉴가 추가되었습니다.\")");
            sb.AppendLine("  ))");
            sb.AppendLine("  (princ)");
            sb.AppendLine(")");
            sb.AppendLine("(civilrun-menu-create)");

            return sb.ToString();
        }

        private static void Sub(StringBuilder sb, string sVar, string iVar, string label)
        {
            sb.AppendLine($"    (setq {sVar} (vla-addsubmenu ri (vla-get-count ri) \"{label}\"))");
            sb.AppendLine($"    (setq {iVar} (vla-get-menuitems {sVar}))");
        }

        private static void Item(StringBuilder sb, string iVar, string label, string cmd) =>
            sb.AppendLine($"    (vla-addmenuitem {iVar} (vla-get-count {iVar}) \"{label}\" \"^C^C{cmd}\")");

        private static void Sep(StringBuilder sb, string iVar) =>
            sb.AppendLine($"    (vla-addseparator {iVar} (vla-get-count {iVar}))");
    }
}
