using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;

namespace DreamPlus.Commands
{
    public class ExportCommands
    {
        // ──────────────────────────────────────────────
        // DP_PLOT_PDF : 현재 레이아웃 또는 모델을 PDF로 출력
        // ──────────────────────────────────────────────
        [CommandMethod("DP_PLOT_PDF", CommandFlags.Session)]
        public void PlotToPdf()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 출력 경로 지정
            var pathRes = ed.GetString(new PromptStringOptions("\nPDF 저장 경로(폴더): ") { AllowSpaces = true });
            if (pathRes.Status != PromptStatus.OK) return;

            var outputDir = pathRes.StringResult.Trim('"');
            if (!Directory.Exists(outputDir))
            {
                ed.WriteMessage($"\n폴더가 존재하지 않습니다: {outputDir}");
                return;
            }

            using var tr = db.TransactionManager.StartTransaction();
            var layouts = new List<Layout>();

            // 모든 레이아웃 수집
            var ltd = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in ltd)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                if (layout.LayoutName != "Model") layouts.Add(layout);
            }

            if (layouts.Count == 0) layouts.Add((Layout)tr.GetObject(db.CurrentLayoutId, OpenMode.ForRead));

            var pm = PlotManager.GetPlotManager(doc);
            int plotted = 0;

            foreach (var layout in layouts)
            {
                var ps = new PlotSettings(layout.ModelType == false);
                ps.CopyFrom(layout);

                var psv = PlotSettingsValidator.Current;
                psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
                psv.SetUseStandardScale(ps, true);
                psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
                psv.SetPlotCentered(ps, true);

                // DWG to PDF PC3 디바이스 사용
                psv.SetPlotConfigurationName(ps, "DWG To PDF.pc3", "ANSI_A_(8.50_x_11.00_Inches)");

                var pi = new PlotInfo { Layout = layout.ObjectId };
                pi.OverrideSettings = ps;

                var piv = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
                piv.Validate(pi);

                var pdfPath = Path.Combine(outputDir, $"{layout.LayoutName}.pdf");

                using (var engine = PlotFactory.CreatePublishEngine())
                {
                    var prog = new PlotProgressDialog(false);
                    prog.set_PlotMsgString(PlotMessageIndex.DialogTitle, "DreamPlus PDF 출력");
                    prog.IsVisible = false;

                    engine.BeginPlot(prog, null);
                    engine.BeginDocument(pi, doc.Name, null, 1, true, pdfPath);
                    engine.BeginPage(pi, prog, true, null);
                    engine.BeginGenerateGraphics(null);
                    engine.EndGenerateGraphics(null);
                    engine.EndPage(null);
                    engine.EndDocument(null);
                    engine.EndPlot(null);
                    plotted++;
                }
            }

            tr.Commit();
            ed.WriteMessage($"\n{plotted}개 레이아웃을 PDF로 저장했습니다. ({outputDir})");
        }

        // ──────────────────────────────────────────────
        // DP_EXPORT_DXF : 현재 도면을 DXF로 내보내기
        // ──────────────────────────────────────────────
        [CommandMethod("DP_EXPORT_DXF")]
        public void ExportDxf()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pathRes = ed.GetString(new PromptStringOptions("\nDXF 저장 경로(.dxf): ") { AllowSpaces = true });
            if (pathRes.Status != PromptStatus.OK) return;

            var path = pathRes.StringResult.Trim('"');
            if (!path.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) path += ".dxf";

            db.DxfOut(path, 16, true);
            ed.WriteMessage($"\nDXF 파일이 저장되었습니다: {path}");
        }

        // ──────────────────────────────────────────────
        // DP_LAYOUT_NEW : 새 레이아웃 생성 (이름 지정)
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYOUT_NEW")]
        public void CreateLayout()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n새 레이아웃 이름: "));
            if (nameRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lm = LayoutManager.Current;

            if (lm.LayoutExists(nameRes.StringResult))
            {
                ed.WriteMessage($"\n'{nameRes.StringResult}' 레이아웃이 이미 존재합니다.");
                return;
            }

            lm.CreateLayout(nameRes.StringResult);
            lm.CurrentLayout = nameRes.StringResult;
            tr.Commit();
            ed.WriteMessage($"\n'{nameRes.StringResult}' 레이아웃이 생성되었습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_LAYOUT_COPY : 레이아웃 복사 (이름 지정)
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYOUT_COPY")]
        public void CopyLayout()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var lm = LayoutManager.Current;

            var srcRes = ed.GetString(new PromptStringOptions("\n복사할 레이아웃 이름: "));
            if (srcRes.Status != PromptStatus.OK) return;

            var dstRes = ed.GetString(new PromptStringOptions("\n새 레이아웃 이름: "));
            if (dstRes.Status != PromptStatus.OK) return;

            if (!lm.LayoutExists(srcRes.StringResult))
            {
                ed.WriteMessage($"\n'{srcRes.StringResult}' 레이아웃이 존재하지 않습니다.");
                return;
            }

            lm.CopyLayout(srcRes.StringResult, dstRes.StringResult);
            ed.WriteMessage($"\n'{srcRes.StringResult}' → '{dstRes.StringResult}' 레이아웃을 복사했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_LAYOUT_LIST : 레이아웃 목록 출력
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYOUT_LIST")]
        public void ListLayouts()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using var tr = db.TransactionManager.StartTransaction();
            var ltd = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);

            ed.WriteMessage("\n──────────────── 레이아웃 목록 ────────────────");
            foreach (DBDictionaryEntry entry in ltd)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                var current = layout.LayoutName == LayoutManager.Current.CurrentLayout ? " ◀ 현재" : "";
                ed.WriteMessage($"\n  {layout.TabOrder,3}. {layout.LayoutName}{current}");
            }
            ed.WriteMessage("\n───────────────────────────────────────────────");
        }
    }
}
