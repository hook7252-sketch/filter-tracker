using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace CivilRun_MadeByHouuu.Commands
{
    public class BlockCommands
    {
        // ──────────────────────────────────────────────
        // DP_BLOCK_INSERT : 외부 블록 삽입 (DWG 파일 → 블록)
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_INSERT")]
        public void InsertBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var pathRes = ed.GetString(new PromptStringOptions("\nDWG 파일 경로: ") { AllowSpaces = true });
            if (pathRes.Status != PromptStatus.OK) return;

            var path = pathRes.StringResult.Trim('"');
            if (!File.Exists(path))
            {
                ed.WriteMessage($"\n파일을 찾을 수 없습니다: {path}");
                return;
            }

            var blockName = Path.GetFileNameWithoutExtension(path);

            // 외부 DWG를 블록으로 가져오기
            using (var sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(path, FileShare.Read, true, null);
                db.Insert(blockName, sourceDb, true);
            }

            // 삽입 위치 지정
            var ptRes = ed.GetPoint(new PromptPointOptions($"\n'{blockName}' 블록 삽입 위치: "));
            if (ptRes.Status != PromptStatus.OK) return;

            var scaleRes = ed.GetDouble(new PromptDoubleOptions("\n축척(기본값 1.0): ") { DefaultValue = 1.0, AllowNone = true });
            double scale = scaleRes.Status == PromptStatus.OK ? scaleRes.Value : 1.0;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var br = new BlockReference(ptRes.Value, ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[blockName])
            {
                ScaleFactors = new Scale3d(scale)
            };

            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            tr.Commit();

            ed.WriteMessage($"\n'{blockName}' 블록을 삽입했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_LIST : 도면 내 블록 목록 출력
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_LIST")]
        public void ListBlocks()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            ed.WriteMessage("\n──────────────── 블록 목록 ────────────────");
            int count = 0;
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (btr.IsAnonymous || btr.IsLayout) continue;
                ed.WriteMessage($"\n  {btr.Name}");
                count++;
            }
            ed.WriteMessage($"\n총 {count}개의 블록\n────────────────────────────────────────");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_COUNT : 선택한 블록 개수 세기
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_COUNT")]
        public void CountBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n세어볼 블록 이름: ") { AllowSpaces = false });
            if (nameRes.Status != PromptStatus.OK) return;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.BlockName, nameRes.StringResult),
            });

            var selRes = ed.SelectAll(filter);
            int count = selRes.Status == PromptStatus.OK ? selRes.Value.Count : 0;
            ed.WriteMessage($"\n'{nameRes.StringResult}' 블록: {count}개");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_EXPLODE_ALL : 선택 영역 블록 전체 분해
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_EXPLODE_ALL")]
        public void ExplodeAllBlocks()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selRes = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n분해할 블록 선택: " },
                new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "INSERT") }));
            if (selRes.Status != PromptStatus.OK) return;

            using var tr = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                if (so == null) continue;
                var br = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as BlockReference;
                if (br == null) continue;

                var exploded = new DBObjectCollection();
                br.Explode(exploded);
                foreach (DBObject obj in exploded)
                {
                    var ent = (Entity)obj;
                    ms.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                }
                br.Erase();
                count++;
            }

            tr.Commit();
            ed.WriteMessage($"\n{count}개의 블록을 분해했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_BLOCK_REPLACE : 블록 이름 일괄 교체
        // ──────────────────────────────────────────────
        [CommandMethod("DP_BLOCK_REPLACE")]
        public void ReplaceBlock()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var oldRes = ed.GetString(new PromptStringOptions("\n교체할 블록 이름: "));
            if (oldRes.Status != PromptStatus.OK) return;

            var newRes = ed.GetString(new PromptStringOptions("\n새 블록 이름: "));
            if (newRes.Status != PromptStatus.OK) return;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.BlockName, oldRes.StringResult),
            });

            var selRes = ed.SelectAll(filter);
            if (selRes.Status != PromptStatus.OK) { ed.WriteMessage("\n대상 블록이 없습니다."); return; }

            using var tr = db.TransactionManager.StartTransaction();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(newRes.StringResult))
            {
                ed.WriteMessage($"\n'{newRes.StringResult}' 블록이 존재하지 않습니다.");
                return;
            }

            var newId = bt[newRes.StringResult];
            int count = 0;
            foreach (SelectedObject so in selRes.Value)
            {
                var br = tr.GetObject(so.ObjectId, OpenMode.ForWrite) as BlockReference;
                if (br == null) continue;
                br.BlockTableRecord = newId;
                count++;
            }

            tr.Commit();
            ed.WriteMessage($"\n{count}개의 블록을 '{newRes.StringResult}'으로 교체했습니다.");
        }
    }
}
