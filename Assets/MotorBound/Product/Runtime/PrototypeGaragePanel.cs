using MotorBound.Vehicle.Core;
using UnityEngine;

namespace MotorBound.Product
{
    public sealed class PrototypeGaragePanel : MonoBehaviour
    {
        private PrototypeGarageSession session;
        private Vector2 scroll;
        private GUIStyle heading;
        private GUIStyle text;
        public void Configure(PrototypeGarageSession target) { session = target; }

        private void OnGUI()
        {
            if (session == null || session.Configuration == null) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
                text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            }

            if (!session.IsInGarage)
            {
                GUILayout.BeginArea(new Rect(16f, Screen.height - 96f, Mathf.Min(730f, Screen.width - 32f), 80f), GUI.skin.box);
                GUILayout.Label(session.Message, text);
                GUILayout.Label("G enter workshop when parked | F6 recover to garage", text);
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, Mathf.Min(470f, Screen.width - 32f), Mathf.Max(180f, Screen.height - 32f)), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("MOTORBOUND / WORKSHOP", heading);
            GUILayout.Label("Kiyora Aven Club • local test garage", text);
            GUILayout.Space(8f);
            var config = session.Configuration;
            GUILayout.Label("Installed: " + config.WheelPackageName, text);
            GUILayout.Label(string.Format("Mass {0:0.0} kg | tires {1:0} mm wide / {2:0} mm radius",
                config.Vehicle.MassKilograms, config.Vehicle.Tire.SectionWidthMeters * 1000d,
                config.Vehicle.Tire.UnloadedRadiusMeters * 1000d), text);
            GUILayout.Label(string.Format("Envelope {0:0.00} m long × {1:0.00} m wide × {2:0.00} m high",
                config.Envelope.OverallLengthMeters, config.Envelope.MaximumWidthMeters, config.Envelope.OverallHeightMeters), text);
            GUILayout.Label("Workshop access: " + session.GarageAccess.Status + " | posted entrance 2.00 m", text);
            GUILayout.Label("Assembly revision " + session.State.Manifest.Revision, text);
            GUILayout.Space(10f);
            GUILayout.Label("Wheel and tire packages", heading);
            foreach (var option in PrototypeGarageCatalog.Options)
            {
                if (GUILayout.Button((option.Key == session.SelectedPackageKey ? "● " : "○ ") + option.DisplayName, GUILayout.MinHeight(32f)))
                    session.SelectPackage(option.Key);
                if (option.Key == session.SelectedPackageKey) GUILayout.Label(option.Description, text);
            }

            var proposal = session.Preview();
            GUILayout.Space(6f);
            GUILayout.Label(proposal.Summary, text);
            if (proposal.Plan != null)
                GUILayout.Label("Proposed bill of materials: " + proposal.Plan.BillOfMaterials.Length + " line(s)", text);
            if (!config.HasCompanionKit && GUILayout.Button("Install touring companion hardware", GUILayout.MinHeight(32f)))
                session.InstallCompanionKit();
            GUI.enabled = proposal.CanInstall;
            if (GUILayout.Button("Install selected package", GUILayout.MinHeight(36f))) session.InstallSelectedPackage();
            GUI.enabled = true;
            GUILayout.Label("Parts are supplied for this prototype. Values are provisional reference data.", text);
            GUILayout.Space(8f);
            GUILayout.Label(session.Message, text);
            GUILayout.Space(8f);
            if (GUILayout.Button("Test drive  [T]", GUILayout.MinHeight(38f))) session.LeaveGarage();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save  [F5]", GUILayout.MinHeight(32f))) session.Save();
            if (GUILayout.Button("Reload  [F9]", GUILayout.MinHeight(32f))) session.Reload();
            GUILayout.EndHorizontal();
            GUILayout.Label("Return to the bay and stop, then press G. F6 recovers a stranded test car.", text);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
