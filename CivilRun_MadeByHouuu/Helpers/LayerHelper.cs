using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CivilRun_MadeByHouuu.Helpers
{
    public static class LayerHelper
    {
        /// <summary>도면에서 실제로 사용 중인 레이어 이름 집합 반환</summary>
        public static HashSet<string> GetUsedLayerNames(Database db)
        {
            var used = new HashSet<string>();
            using var tr = db.TransactionManager.StartTransaction();

            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForRead);

            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null) used.Add(ent.Layer);
            }

            // 페이퍼 스페이스 포함
            var ltd = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in ltd)
            {
                var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null) used.Add(ent.Layer);
                }
            }

            tr.Commit();
            return used;
        }
    }
}
