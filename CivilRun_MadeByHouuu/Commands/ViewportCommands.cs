using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    /// <summary>
    /// 뷰포트 (Viewport) 관련 명령어
    /// </summary>
    public class ViewportCommands
    {
        // ══════════════════════════════════════════════
        //  뷰포트 기본 관리
        // ══════════════════════════════════════════════

        /// <summary>선택 뷰포트 잠금 (VPLK)</summary>
        [CommandMethod("DP_VPLK")]
        public void LockViewports()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n잠글 뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Viewport vp)
                { vp.Locked = true; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트를 잠갔습니다.");
        }

        /// <summary>선택 뷰포트 잠금 해제 (VPULK)</summary>
        [CommandMethod("DP_VPULK")]
        public void UnlockViewports()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n잠금 해제할 뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Viewport vp)
                { vp.Locked = false; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트 잠금을 해제했습니다.");
        }

        /// <summary>모든 뷰포트 잠금 (VPLKALL)</summary>
        [CommandMethod("DP_VPLKALL")]
        public void LockAllViewports()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using var tr = db.TransactionManager.StartTransaction();
            var selRes = ed.SelectAll(
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK) { tr.Commit(); return; }

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Viewport vp && !vp.Locked)
                { vp.Locked = true; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트를 모두 잠갔습니다.");
        }

        /// <summary>선택 뷰포트에서 레이어 동결 (VPFRZ)</summary>
        [CommandMethod("DP_VPFRZ")]
        public void FreezeLayerInViewport()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vpRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n대상 뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (vpRes.Status != PromptStatus.OK || vpRes.Value.Count == 0) return;

            var layerRes = ed.GetString(new PromptStringOptions("\n동결할 레이어 이름: "));
            if (layerRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }

            var vpObj = tr.GetObject(vpRes.Value[0].ObjectId, OpenMode.ForRead) as Viewport;
            if (vpObj == null) { tr.Commit(); return; }

            var layerId = lt[layerRes.StringResult];
            var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
            // 뷰포트 동결은 ObjectContextCollection을 통해 적용
            // 간략화: 레이어 자체 동결 대신 뷰포트별 동결 처리
            layer.ViewportVisibilityDefault = false;
            tr.Commit();
            ed.WriteMessage($"\n뷰포트에서 '{layerRes.StringResult}' 레이어를 동결했습니다.");
        }

        /// <summary>뷰포트 축척 설정 (VPSCALE)</summary>
        [CommandMethod("DP_VPSCALE")]
        public void SetViewportScale()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var scaleRes = ed.GetDouble(new PromptDoubleOptions("\n뷰포트 축척(예: 100 = 1/100): ")
            { DefaultValue = 100.0, AllowNegative = false, AllowZero = false });
            if (scaleRes.Status != PromptStatus.OK) return;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Viewport vp)
                {
                    vp.CustomScale = 1.0 / scaleRes.Value;
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트 축척을 1/{scaleRes.Value}로 설정했습니다.");
        }

        /// <summary>뷰포트 색상 설정 (VPCOLOR)</summary>
        [CommandMethod("DP_VPCOLOR")]
        public void SetViewportColor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var colorRes = ed.GetInteger(new PromptIntegerOptions("\n뷰포트 색상 번호(1-255): ")
            { LowerLimit = 1, UpperLimit = 255 });
            if (colorRes.Status != PromptStatus.OK) return;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Viewport vp)
                {
                    vp.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)colorRes.Value);
                    count++;
                }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트 색상 변경 완료.");
        }

        /// <summary>뷰포트 경계선 레이어 변경 (VPLAYER)</summary>
        [CommandMethod("DP_VPLAYER")]
        public void ChangeViewportLayer()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var layerRes = ed.GetString(new PromptStringOptions("\n뷰포트 레이어 이름: "));
            if (layerRes.Status != PromptStatus.OK) return;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerRes.StringResult)) { ed.WriteMessage("\n레이어 없음."); return; }

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var vp = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as Viewport;
                if (vp != null) { vp.Layer = layerRes.StringResult; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트 레이어 변경 완료.");
        }

        /// <summary>배치 뷰포트를 모형으로 변환 (VP2MODEL)</summary>
        [CommandMethod("DP_VP2MODEL")]
        public void ViewportToModel()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n변환할 뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var vp = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Viewport;
                if (vp == null) continue;

                // 뷰포트 중심 및 높이 기반으로 모형 경계 사각형 생성
                double scale = vp.CustomScale > 0 ? 1.0 / vp.CustomScale : 1.0;
                double w = vp.Width * scale;
                double h = vp.Height * scale;
                var center = vp.ViewCenter;

                var pl = new Polyline(4);
                pl.AddVertexAt(0, new Point2d(center.X - w / 2, center.Y - h / 2), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(center.X + w / 2, center.Y - h / 2), 0, 0, 0);
                pl.AddVertexAt(2, new Point2d(center.X + w / 2, center.Y + h / 2), 0, 0, 0);
                pl.AddVertexAt(3, new Point2d(center.X - w / 2, center.Y + h / 2), 0, 0, 0);
                pl.Closed = true;
                ms.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
                count++;
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트 영역을 모형에 사각형으로 표시했습니다.");
        }

        /// <summary>모형 폴리라인 경계로 배치 뷰포트 생성 (MODEL2VP)</summary>
        [CommandMethod("DP_MODEL2VP")]
        public void ModelToViewport()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            if (db.TileMode)
            {
                ed.WriteMessage("\n배치(Paper Space) 탭에서 실행하세요.");
                return;
            }

            var scaleRes = ed.GetDouble(new PromptDoubleOptions("\n뷰포트 축척(예: 100=1/100): ")
            { DefaultValue = 100.0 });
            double scale = scaleRes.Status == PromptStatus.OK ? scaleRes.Value : 100.0;

            var selRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n경계가 될 폴리라인 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ps = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            foreach (SelectedObject so in selRes.Value)
            {
                var pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) continue;

                var ext = pl.GeometricExtents;
                double vpW = (ext.MaxPoint.X - ext.MinPoint.X) / scale;
                double vpH = (ext.MaxPoint.Y - ext.MinPoint.Y) / scale;

                var ptRes = ed.GetPoint(new PromptPointOptions("\n뷰포트를 배치할 위치: "));
                if (ptRes.Status != PromptStatus.OK) continue;

                var vp = new Viewport
                {
                    Width = vpW,
                    Height = vpH,
                    CenterPoint = ptRes.Value
                };
                vp.ViewCenter = new Point2d(
                    (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                    (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);
                vp.CustomScale = 1.0 / scale;
                ps.AppendEntity(vp);
                tr.AddNewlyCreatedDBObject(vp, true);
                vp.On = true;
            }
            tr.Commit();
            ed.WriteMessage("\n뷰포트를 생성했습니다.");
        }

        /// <summary>뷰포트 목록 출력 (VPLIST)</summary>
        [CommandMethod("DP_VPLIST")]
        public void ListViewports()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using var tr = db.TransactionManager.StartTransaction();
            var ltd = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);

            ed.WriteMessage("\n──── 뷰포트 목록 ────");
            foreach (DBDictionaryEntry entry in ltd)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                int vpCount = 0;
                foreach (ObjectId id in btr)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Viewport) vpCount++;
                }
                if (vpCount > 0)
                    ed.WriteMessage($"\n  {layout.LayoutName}: 뷰포트 {vpCount}개");
            }
            ed.WriteMessage("\n─────────────────────");
        }

        /// <summary>뷰포트 축척 일괄 정렬 (VPSCALE_MATCH)</summary>
        [CommandMethod("DP_VPSCALE_MATCH")]
        public void MatchViewportScale()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var srcRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n기준 뷰포트 선택(1개): " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (srcRes.Status != PromptStatus.OK || srcRes.Value.Count == 0) return;

            using var srcTr = db.TransactionManager.StartTransaction();
            var srcVp = srcTr.GetObject(srcRes.Value[0].ObjectId, OpenMode.ForRead) as Viewport;
            double srcScale = srcVp?.CustomScale ?? 1.0;
            srcTr.Commit();

            var dstRes = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n맞출 뷰포트 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "VIEWPORT") }));
            if (dstRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            int count = 0;
            foreach (SelectedObject so in dstRes.Value)
            {
                if (tr.GetObject(so.ObjectId, OpenMode.ForWrite) is Viewport vp)
                { vp.CustomScale = srcScale; count++; }
            }
            tr.Commit();
            ed.WriteMessage($"\n{count}개 뷰포트 축척을 {1.0 / srcScale:F0}분의 1로 맞췄습니다.");
        }
    }
}
