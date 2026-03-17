using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace DreamPlus.Commands
{
    public class TextCommands
    {
        // ──────────────────────────────────────────────
        // DP_TEXT_HEIGHT : 선택 텍스트 높이 일괄 변경
        // ──────────────────────────────────────────────
        [CommandMethod("DP_TEXT_HEIGHT")]
        public void ChangeTextHeight()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "TEXT,MTEXT"),
            });

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;

            var heightRes = ed.GetDouble(new PromptDoubleOptions("\n새 텍스트 높이: ") { AllowNegative = false, AllowZero = false });
            if (heightRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.Height = heightRes.Value; count++; }
                else if (obj is MText mt) { mt.TextHeight = heightRes.Value; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 높이를 {heightRes.Value}로 변경했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_TEXT_STYLE : 선택 텍스트 스타일 일괄 변경
        // ──────────────────────────────────────────────
        [CommandMethod("DP_TEXT_STYLE")]
        public void ChangeTextStyle()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var styleRes = ed.GetString(new PromptStringOptions("\n텍스트 스타일 이름: "));
            if (styleRes.Status != PromptStatus.OK) return;

            using var checkTr = db.TransactionManager.StartTransaction();
            var tt = (TextStyleTable)checkTr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!tt.Has(styleRes.StringResult))
            {
                ed.WriteMessage($"\n'{styleRes.StringResult}' 스타일이 존재하지 않습니다.");
                return;
            }
            var styleId = tt[styleRes.StringResult];
            checkTr.Commit();

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var obj = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                if (obj is DBText dt) { dt.TextStyleId = styleId; count++; }
                else if (obj is MText mt) { mt.TextStyleId = styleId; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트 스타일을 '{styleRes.StringResult}'으로 변경했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_TEXT_LAYER : 선택 텍스트 레이어 일괄 이동
        // ──────────────────────────────────────────────
        [CommandMethod("DP_TEXT_LAYER")]
        public void MoveTextToLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var layerRes = ed.GetString(new PromptStringOptions("\n이동할 레이어 이름: "));
            if (layerRes.Status != PromptStatus.OK) return;

            using var checkTr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)checkTr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerRes.StringResult))
            {
                ed.WriteMessage($"\n'{layerRes.StringResult}' 레이어가 존재하지 않습니다.");
                return;
            }
            checkTr.Commit();

            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n텍스트 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var ent = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                ent.Layer = layerRes.StringResult;
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 텍스트를 '{layerRes.StringResult}' 레이어로 이동했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_DIM_STYLE : 선택 치수 스타일 일괄 변경
        // ──────────────────────────────────────────────
        [CommandMethod("DP_DIM_STYLE")]
        public void ChangeDimStyle()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var styleRes = ed.GetString(new PromptStringOptions("\n치수 스타일 이름: "));
            if (styleRes.Status != PromptStatus.OK) return;

            using var checkTr = db.TransactionManager.StartTransaction();
            var dst = (DimStyleTable)checkTr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!dst.Has(styleRes.StringResult))
            {
                ed.WriteMessage($"\n'{styleRes.StringResult}' 치수 스타일이 존재하지 않습니다.");
                return;
            }
            var styleId = dst[styleRes.StringResult];
            checkTr.Commit();

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start,
                    "DIMENSION,ROTATED,ALIGNED,RADIAL,DIAMETRIC,ANGULAR,ORDINATE,LEADER"),
            });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n치수 선택: " }, filter);
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Dimension dim)
                {
                    dim.DimensionStyleId = styleId;
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 치수 스타일을 '{styleRes.StringResult}'으로 변경했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_DIM_SCALE : 치수 전체 축척 변경
        // ──────────────────────────────────────────────
        [CommandMethod("DP_DIM_SCALE")]
        public void ChangeDimScale()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var scaleRes = ed.GetDouble(new PromptDoubleOptions("\n치수 전체 축척: ")
                { DefaultValue = 1.0, AllowNegative = false, AllowZero = false });
            if (scaleRes.Status != PromptStatus.OK) return;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "DIMENSION,ROTATED,ALIGNED,RADIAL,DIAMETRIC,ANGULAR,ORDINATE"),
            });
            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n치수 선택 (Enter=전체): " }, filter);
            if (selRes.Status == PromptStatus.Error) selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Dimension dim)
                {
                    // Dimscale은 DimStyleTableRecord를 통해 override
                    var dsr = dim.GetDimVarContainer();
                    dsr.Dimscale = scaleRes.Value;
                    dim.RecomputeDimensionBlock(true);
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 치수 축척을 {scaleRes.Value}로 변경했습니다.");
        }
    }
}
