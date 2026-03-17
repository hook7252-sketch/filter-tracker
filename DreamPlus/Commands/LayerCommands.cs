using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using DreamPlus.Helpers;

namespace DreamPlus.Commands
{
    public class LayerCommands
    {
        // ──────────────────────────────────────────────
        // DP_LAYER_NEW : 레이어 일괄 생성
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYER_NEW")]
        public void CreateLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var opt = new PromptStringOptions("\n레이어 이름(쉼표로 구분): ") { AllowSpaces = true };
            var res = ed.GetString(opt);
            if (res.Status != PromptStatus.OK) return;

            var names = res.StringResult.Split(',')
                           .Select(n => n.Trim())
                           .Where(n => n.Length > 0);

            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

            int created = 0;
            foreach (var name in names)
            {
                if (lt.Has(name)) { ed.WriteMessage($"\n'{name}' 레이어가 이미 존재합니다."); continue; }

                lt.UpgradeOpen();
                var layer = new LayerTableRecord { Name = name };
                lt.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
                created++;
            }

            tr.Commit();
            ed.WriteMessage($"\n{created}개의 레이어를 생성했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_LAYER_COLOR : 레이어 색상 일괄 변경
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYER_COLOR")]
        public void ChangeLayerColor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var nameRes = ed.GetString(new PromptStringOptions("\n레이어 이름: ") { AllowSpaces = false });
            if (nameRes.Status != PromptStatus.OK) return;

            var colorRes = ed.GetInteger(new PromptIntegerOptions("\n색상 번호(1-255): ") { LowerLimit = 1, UpperLimit = 255 });
            if (colorRes.Status != PromptStatus.OK) return;

            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

            if (!lt.Has(nameRes.StringResult))
            {
                ed.WriteMessage($"\n'{nameRes.StringResult}' 레이어를 찾을 수 없습니다.");
                return;
            }

            var layer = (LayerTableRecord)tr.GetObject(lt[nameRes.StringResult], OpenMode.ForWrite);
            layer.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)colorRes.Value);
            tr.Commit();
            ed.WriteMessage($"\n'{nameRes.StringResult}' 레이어 색상을 {colorRes.Value}번으로 변경했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_LAYER_FREEZE_OTHERS : 현재 레이어 외 모두 동결
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYER_FREEZE_OTHERS")]
        public void FreezeOtherLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            var currentName = ((LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead)).Name;

            int count = 0;
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (layer.Name == currentName || layer.Name == "0") continue;
                if (!layer.IsFrozen) { layer.IsFrozen = true; count++; }
            }

            tr.Commit();
            ed.WriteMessage($"\n{count}개의 레이어를 동결했습니다. (현재 레이어: {currentName})");
        }

        // ──────────────────────────────────────────────
        // DP_LAYER_THAW_ALL : 모든 레이어 동결 해제
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYER_THAW_ALL")]
        public void ThawAllLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

            int count = 0;
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                if (layer.IsFrozen) { layer.IsFrozen = false; count++; }
            }

            tr.Commit();
            doc.Editor.WriteMessage($"\n{count}개의 레이어 동결을 해제했습니다.");
        }

        // ──────────────────────────────────────────────
        // DP_LAYER_LIST : 레이어 목록 출력
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYER_LIST")]
        public void ListLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using var tr = doc.Database.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);

            ed.WriteMessage("\n──────────────── 레이어 목록 ────────────────");
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                var status = layer.IsFrozen ? "동결" : layer.IsOff ? "꺼짐" : "켜짐";
                ed.WriteMessage($"\n  {layer.Name,-20} 색상:{layer.Color.ColorIndex,3}  상태:{status}");
            }
            ed.WriteMessage("\n─────────────────────────────────────────────");
        }

        // ──────────────────────────────────────────────
        // DP_LAYER_DELETE_EMPTY : 빈 레이어 일괄 삭제
        // ──────────────────────────────────────────────
        [CommandMethod("DP_LAYER_DELETE_EMPTY")]
        public void DeleteEmptyLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var used = LayerHelper.GetUsedLayerNames(db);

            using var tr = db.TransactionManager.StartTransaction();
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            var toDelete = new List<ObjectId>();
            foreach (ObjectId id in lt)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (layer.Name is "0" or "Defpoints") continue;
                if (!used.Contains(layer.Name)) toDelete.Add(id);
            }

            foreach (var id in toDelete)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                layer.Erase();
            }

            tr.Commit();
            ed.WriteMessage($"\n{toDelete.Count}개의 빈 레이어를 삭제했습니다.");
        }
    }
}
