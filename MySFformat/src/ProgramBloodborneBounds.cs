using System;
using System.IO;
using System.Linq;
using SoulsFormats;
using System.Windows.Forms;

namespace MySFformat
{
    static partial class Program
    {
        private static void BB_BoneBoundingBOX_Calc_Click()
        {
            if (targetFlver == null) return;
            if (MessageBox.Show(nodeUIForm,
                "Calculate Bloodborne bone-local bounding boxes from referenced bind-pose vertices?\r\n\r\n" +
                "This can fix whole meshes disappearing at certain camera angles/positions when node bounds are invalid. " +
                "It does not change Header/mesh bounds, textures, geometry, weights, skeleton transforms or Cloth. " +
                "Existing valid margins are preserved. Invalid unused nodes receive a zero-size local box.\r\n\r\n" +
                "Only the current model in memory is updated. Save/export a separate copy afterwards. " +
                "Rest-pose calculation is not a guarantee for arbitrary Cloth excursions or every animation.",
                "BB_BoneBoundingBOX_Calc", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            bool applied = false;
            try
            {
                var result = BloodborneBoneBounds.Calculate(targetFlver);
                result.Apply(targetFlver);
                applied = true;
                updateVertices();
                ForceRefreshNodes();
                if (tbones != null && !tbones.IsDisposed)
                    tbones.Text = FormatOutput(new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(targetFlver.Nodes));
                var save = MessageBox.Show(nodeUIForm,
                    "Calculated node-local bounds.\r\nChanged nodes: " + result.ChangedNodes +
                    "\r\nGeometry/ancestor/owner nodes: " + result.GeometryNodes +
                    "\r\nInvalid unused nodes repaired: " + result.RepairedUnusedNodes +
                    "\r\nReferenced vertices: " + result.ReferencedVertices +
                    "\r\nEmpty meshes skipped: " + result.SkippedEmptyMeshes +
                    "\r\n\r\nNot written to disk. Save a NEW FLVER copy now?\r\n" +
                    "No keeps the calculated bounds in memory. Existing files will not be overwritten.",
                    "BB_BoneBoundingBOX_Calc", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (save == DialogResult.Yes) SaveBloodborneBoundsCopy();
            }
            catch (Exception ex)
            {
                MessageBox.Show(nodeUIForm, (applied ? "Bounds were calculated in memory, but refresh/save failed. No existing file was overwritten:\r\n" :
                    "Bone bounds calculation failed; model was not changed:\r\n") + ex.Message,
                    "BB_BoneBoundingBOX_Calc", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SaveBloodborneBoundsCopy()
        {
            using (var dialog = new SaveFileDialog
            {
                Title = "Save NEW FLVER copy - calculated BB node bounds",
                Filter = "FLVER model (*.flver)|*.flver",
                DefaultExt = "flver", AddExtension = true, OverwritePrompt = true,
                FileName = Path.GetFileNameWithoutExtension(flverName) + "-bb-bounds.flver"
            })
            {
                if (dialog.ShowDialog(nodeUIForm) != DialogResult.OK) return;
                // Never let the native overwrite confirmation authorize replacing an existing asset.
                if (File.Exists(dialog.FileName))
                    throw new IOException("The output already exists. Choose a NEW filename; no file was overwritten.");
                byte[] bytes = targetFlver.Write();
                if (!FLVER2.Read(bytes).Write().SequenceEqual(bytes))
                    throw new InvalidDataException("FLVER write/readback verification failed. No file was created.");
                using (var stream = new FileStream(dialog.FileName, FileMode.CreateNew, FileAccess.Write))
                    stream.Write(bytes, 0, bytes.Length);
                MessageBox.Show(nodeUIForm, "Saved a new FLVER copy:\r\n" + dialog.FileName +
                    "\r\n\r\nThe editor's original file path was not changed.", "BB_BoneBoundingBOX_Calc",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
