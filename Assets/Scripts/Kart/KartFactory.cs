using System.Collections.Generic;
using UnityEngine;
using VortexKarts.Audio;
using VortexKarts.Data;
using VortexKarts.PowerUps;
using VortexKarts.Race;
using VortexKarts.Utils;

namespace VortexKarts.Kart
{
    /// <summary>
    /// Builds a complete kart (physics root, procedural body, pilot, anchors and all gameplay components).
    /// The visual origin sits at ground level under the sphere so wheels touch the road.
    /// </summary>
    public static class KartFactory
    {
        private static PhysicsMaterial kartPhysicsMaterial;

        private static PhysicsMaterial KartPhysicsMaterial
        {
            get
            {
                if (kartPhysicsMaterial == null)
                {
                    kartPhysicsMaterial = new PhysicsMaterial("KartSphere");
                    kartPhysicsMaterial.dynamicFriction = 0f;
                    kartPhysicsMaterial.staticFriction = 0f;
                    kartPhysicsMaterial.bounciness = 0.2f;
                    kartPhysicsMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
                    kartPhysicsMaterial.bounceCombine = PhysicsMaterialCombine.Average;
                }
                return kartPhysicsMaterial;
            }
        }

        public static KartController CreateKart(KartStats stats, PilotData pilot, bool isPlayer, Vector3 groundPosition,
            Quaternion rotation, int index)
        {
            string name = pilot != null ? pilot.displayName : (isPlayer ? "Player" : "CPU " + index);
            var root = new GameObject("Kart_" + name);
            root.layer = Layers.Kart;
            root.transform.position = groundPosition + Vector3.up * KartController.Radius;
            root.transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);

            var rb = root.AddComponent<Rigidbody>();
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = KartController.Radius;
            sphere.center = Vector3.zero;
            sphere.sharedMaterial = KartPhysicsMaterial;

            var controller = root.AddComponent<KartController>();
            controller.Index = index;
            root.AddComponent<DriftController>();
            root.AddComponent<BoostController>();
            root.AddComponent<JumpController>();
            root.AddComponent<KartStatusEffects>();
            root.AddComponent<RespawnController>();
            var visuals = root.AddComponent<KartVisuals>();
            var feedback = root.AddComponent<KartFeedback>();
            root.AddComponent<RaceProgressTracker>();
            root.AddComponent<PowerUpInventory>();
            var audio = root.AddComponent<VehicleAudio>();

            // Orientation carries everything that rotates with the simulated heading.
            var orientation = new GameObject("Orientation").transform;
            orientation.SetParent(root.transform, false);
            controller.Orientation = orientation;

            var visualRoot = new GameObject("Visual").transform;
            visualRoot.SetParent(orientation, false);
            visualRoot.localPosition = new Vector3(0f, -KartController.Radius + 0.01f, 0f);
            controller.VisualRoot = visualRoot;

            BuildBody(controller, visuals, visualRoot, stats, pilot, isPlayer);

            controller.Initialize(stats, pilot, isPlayer, name);
            feedback.Build(controller);
            audio.Build(controller);

            if (isPlayer) root.AddComponent<PlayerKartDriver>();
            return controller;
        }

        private static void BuildBody(KartController controller, KartVisuals visuals, Transform visualRoot, KartStats stats,
            PilotData pilot, bool isPlayer)
        {
            Color primary = pilot != null ? pilot.primaryColor : stats.bodyColor;
            Color secondary = pilot != null ? pilot.secondaryColor : stats.accentColor;
            Color dark = new Color(0.12f, 0.12f, 0.14f);
            Color tyre = new Color(0.08f, 0.08f, 0.09f);
            Vector3 bs = stats.bodyScale;

            Material matPrimary = MaterialLibrary.Lit(primary, 0.55f, 0.1f);
            Material matSecondary = MaterialLibrary.Lit(secondary, 0.5f, 0.05f);
            Material matDark = MaterialLibrary.Lit(dark, 0.35f, 0.2f);
            Material matTyre = MaterialLibrary.Lit(tyre, 0.2f, 0f);
            Material matMetal = MaterialLibrary.Lit(new Color(0.6f, 0.62f, 0.66f), 0.7f, 0.8f);

            // ---- Chassis group (leans, spins visually)
            var chassis = new GameObject("Chassis").transform;
            chassis.SetParent(visualRoot, false);
            visuals.Chassis = chassis;

            PrimitiveFactory.Box("Body", chassis, new Vector3(0f, 0.32f, 0.05f), new Vector3(1.15f * bs.x, 0.22f * bs.y, 1.85f * bs.z), matPrimary);
            PrimitiveFactory.Box("Nose", chassis, new Vector3(0f, 0.36f, 0.98f * bs.z), new Vector3(0.7f * bs.x, 0.14f, 0.5f), matSecondary);
            PrimitiveFactory.Box("PodL", chassis, new Vector3(-0.58f * bs.x, 0.3f, -0.2f), new Vector3(0.24f, 0.2f, 0.95f), matSecondary);
            PrimitiveFactory.Box("PodR", chassis, new Vector3(0.58f * bs.x, 0.3f, -0.2f), new Vector3(0.24f, 0.2f, 0.95f), matSecondary);
            PrimitiveFactory.Box("Engine", chassis, new Vector3(0f, 0.5f, -0.72f * bs.z), new Vector3(0.6f, 0.3f, 0.42f), matDark);
            PrimitiveFactory.Box("RearBumper", chassis, new Vector3(0f, 0.3f, -1.02f * bs.z), new Vector3(1.25f * bs.x, 0.12f, 0.12f), matDark);
            PrimitiveFactory.Box("FrontBumper", chassis, new Vector3(0f, 0.3f, 1.12f * bs.z), new Vector3(0.95f * bs.x, 0.1f, 0.1f), matDark);
            PrimitiveFactory.Box("Number", chassis, new Vector3(0f, 0.44f, 0.55f * bs.z), new Vector3(0.36f, 0.02f, 0.36f), matSecondary);

            // Wheels: pivot (steer) -> spinner (roll) -> mesh.
            var frontPivots = new List<Transform>();
            var spinners = new List<Transform>();
            var rearAnchors = new List<Transform>();
            float wx = 0.66f * bs.x;
            Vector3[] wheelPos =
            {
                new Vector3(-wx, 0.26f, 0.72f * bs.z), new Vector3(wx, 0.26f, 0.72f * bs.z),
                new Vector3(-wx, 0.26f, -0.66f * bs.z), new Vector3(wx, 0.26f, -0.66f * bs.z)
            };
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                var pivot = new GameObject(front ? "FrontWheelPivot" : "RearWheelPivot").transform;
                pivot.SetParent(chassis, false);
                pivot.localPosition = wheelPos[i];
                var spinner = new GameObject("Spinner").transform;
                spinner.SetParent(pivot, false);
                // Cylinder axis is Y; rotate so the axle runs along X.
                PrimitiveFactory.Cylinder("Tyre", spinner, Vector3.zero, 0.52f, 0.26f, matTyre, false, Quaternion.Euler(0f, 0f, 90f));
                PrimitiveFactory.Cylinder("Hub", spinner, Vector3.zero, 0.3f, 0.28f, matSecondary, false, Quaternion.Euler(0f, 0f, 90f));
                spinners.Add(spinner);
                if (front) frontPivots.Add(pivot);
                else
                {
                    var anchor = new GameObject("RearWheelAnchor").transform;
                    anchor.SetParent(visualRoot, false);
                    anchor.localPosition = wheelPos[i] + new Vector3(0f, -0.2f, -0.2f);
                    rearAnchors.Add(anchor);
                }
            }
            visuals.FrontWheelPivots = frontPivots.ToArray();
            visuals.WheelSpinners = spinners.ToArray();
            controller.RearWheelAnchors = rearAnchors.ToArray();

            // Exhausts.
            var exhausts = new List<Transform>();
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.2f : 0.2f;
                PrimitiveFactory.Cylinder("Exhaust", chassis, new Vector3(x, 0.5f, -0.98f * bs.z), 0.12f, 0.3f, matMetal, false,
                    Quaternion.Euler(90f, 0f, 0f));
                var anchor = new GameObject("ExhaustAnchor").transform;
                anchor.SetParent(visualRoot, false);
                anchor.localPosition = new Vector3(x, 0.5f, -1.12f * bs.z);
                exhausts.Add(anchor);
            }
            controller.ExhaustAnchors = exhausts.ToArray();

            // ---- Cockpit group (does not spin with the chassis so the VR seat stays put)
            var cockpit = new GameObject("Cockpit").transform;
            cockpit.SetParent(visualRoot, false);

            PrimitiveFactory.Box("Seat", cockpit, new Vector3(0f, 0.46f, -0.3f), new Vector3(0.55f, 0.28f, 0.45f), matDark);
            PrimitiveFactory.Box("Backrest", cockpit, new Vector3(0f, 0.72f, -0.52f), new Vector3(0.55f, 0.5f, 0.1f), matDark);
            PrimitiveFactory.Cylinder("Column", cockpit, new Vector3(0f, 0.66f, 0.38f), 0.05f, 0.4f, matMetal, false, Quaternion.Euler(40f, 0f, 0f));

            var wheelPivot = new GameObject("SteeringWheel").transform;
            wheelPivot.SetParent(cockpit, false);
            wheelPivot.localPosition = new Vector3(0f, 0.82f, 0.42f);
            var wheelBase = Quaternion.Euler(-65f, 0f, 0f);
            wheelPivot.localRotation = wheelBase;
            PrimitiveFactory.Cylinder("Rim", wheelPivot, Vector3.zero, 0.36f, 0.035f, matDark);
            PrimitiveFactory.Box("Spoke", wheelPivot, Vector3.zero, new Vector3(0.34f, 0.03f, 0.06f), matSecondary);
            PrimitiveFactory.Box("Spoke2", wheelPivot, new Vector3(0f, 0f, -0.08f), new Vector3(0.06f, 0.03f, 0.16f), matSecondary);
            var leftGrip = new GameObject("GripL").transform;
            leftGrip.SetParent(wheelPivot, false);
            leftGrip.localPosition = new Vector3(-0.17f, 0.02f, 0f);
            var rightGrip = new GameObject("GripR").transform;
            rightGrip.SetParent(wheelPivot, false);
            rightGrip.localPosition = new Vector3(0.17f, 0.02f, 0f);
            visuals.SteeringWheel = wheelPivot;
            visuals.SteeringWheelBaseRotation = wheelBase;

            var dash = PrimitiveFactory.Box("Dashboard", cockpit, new Vector3(0f, 0.8f, 0.62f), new Vector3(0.72f, 0.18f, 0.08f), matDark,
                false, Quaternion.Euler(25f, 0f, 0f));
            var dashAnchor = new GameObject("DashboardAnchor").transform;
            dashAnchor.SetParent(cockpit, false);
            dashAnchor.localPosition = new Vector3(0f, 0.95f, 0.72f);
            dashAnchor.localRotation = Quaternion.Euler(32f, 0f, 0f);
            controller.DashboardAnchor = dashAnchor;

            // Eye anchor: a little above the pilot's head centre so the road is visible over the wheel.
            var seatAnchor = new GameObject("SeatAnchor").transform;
            seatAnchor.SetParent(cockpit, false);
            seatAnchor.localPosition = new Vector3(0f, 1.16f, -0.3f);
            controller.SeatAnchor = seatAnchor;

            // ---- Pilot
            var pilotRoot = new GameObject("Pilot");
            pilotRoot.transform.SetParent(visualRoot, false);
            var rig = pilotRoot.AddComponent<PilotRig>();
            BuildPilot(rig, pilotRoot.transform, pilot, matPrimary, matSecondary, matDark, leftGrip, rightGrip);
            rig.Bind(controller);
            rig.SetHeadVisible(!isPlayer);
            visuals.Pilot = rig;

            PrimitiveFactory.SetShadowCasting(dash, false, true);
        }

        private static void BuildPilot(PilotRig rig, Transform root, PilotData pilot, Material matPrimary, Material matSecondary,
            Material matDark, Transform leftGrip, Transform rightGrip)
        {
            Color skin = pilot != null ? pilot.skinColor : new Color(0.9f, 0.75f, 0.6f);
            Material matSkin = MaterialLibrary.Lit(skin, 0.3f, 0f);
            Material matSuit = MaterialLibrary.Lit(pilot != null ? pilot.primaryColor * 0.85f : Color.gray, 0.4f, 0f);
            HelmetStyle helmet = pilot != null ? pilot.helmet : HelmetStyle.Round;

            var torso = PrimitiveFactory.Capsule("Torso", root, new Vector3(0f, 0.66f, -0.3f), 0.42f, 0.6f, matSuit);
            rig.Torso = torso.transform;

            var headPivot = new GameObject("HeadPivot").transform;
            headPivot.SetParent(root, false);
            headPivot.localPosition = new Vector3(0f, 0.92f, -0.24f);
            rig.HeadPivot = headPivot;

            var headRenderers = new List<Renderer>();
            var head = PrimitiveFactory.Sphere("Head", headPivot, new Vector3(0f, 0.1f, 0.02f), 0.3f, matSkin);
            rig.Head = head.transform;
            headRenderers.Add(head.GetComponent<Renderer>());

            var helmetGo = PrimitiveFactory.Sphere("Helmet", headPivot, new Vector3(0f, 0.13f, 0f), 0.34f, matPrimary);
            headRenderers.Add(helmetGo.GetComponent<Renderer>());
            var visor = PrimitiveFactory.Box("Visor", headPivot, new Vector3(0f, 0.09f, 0.14f), new Vector3(0.26f, 0.1f, 0.08f), matDark);
            headRenderers.Add(visor.GetComponent<Renderer>());
            switch (helmet)
            {
                case HelmetStyle.Visor:
                    var bigVisor = PrimitiveFactory.Box("VisorWide", headPivot, new Vector3(0f, 0.1f, 0.15f), new Vector3(0.34f, 0.14f, 0.06f), matSecondary);
                    headRenderers.Add(bigVisor.GetComponent<Renderer>());
                    break;
                case HelmetStyle.Crest:
                    var crest = PrimitiveFactory.Box("Crest", headPivot, new Vector3(0f, 0.32f, -0.02f), new Vector3(0.06f, 0.14f, 0.3f), matSecondary);
                    headRenderers.Add(crest.GetComponent<Renderer>());
                    break;
                case HelmetStyle.Antenna:
                    var stalk = PrimitiveFactory.Cylinder("Antenna", headPivot, new Vector3(0.1f, 0.36f, 0f), 0.03f, 0.2f, matDark);
                    var ball = PrimitiveFactory.Sphere("AntennaBall", headPivot, new Vector3(0.1f, 0.47f, 0f), 0.08f, matSecondary);
                    headRenderers.Add(stalk.GetComponent<Renderer>());
                    headRenderers.Add(ball.GetComponent<Renderer>());
                    break;
            }
            rig.HeadRenderers = headRenderers.ToArray();

            rig.LeftShoulder = new Vector3(-0.2f, 0.82f, -0.25f);
            rig.RightShoulder = new Vector3(0.2f, 0.82f, -0.25f);
            rig.LeftArm = PrimitiveFactory.Capsule("ArmL", root, rig.LeftShoulder, 0.075f, 0.5f, matSuit).transform;
            rig.RightArm = PrimitiveFactory.Capsule("ArmR", root, rig.RightShoulder, 0.075f, 0.5f, matSuit).transform;
            rig.LeftHand = PrimitiveFactory.Sphere("HandL", root, Vector3.zero, 0.11f, matDark).transform;
            rig.RightHand = PrimitiveFactory.Sphere("HandR", root, Vector3.zero, 0.11f, matDark).transform;
            rig.LeftGrip = leftGrip;
            rig.RightGrip = rightGrip;

            // Legs (static) towards the pedals.
            Vector3 hipL = new Vector3(-0.12f, 0.46f, -0.2f), footL = new Vector3(-0.12f, 0.38f, 0.5f);
            Vector3 hipR = new Vector3(0.12f, 0.46f, -0.2f), footR = new Vector3(0.12f, 0.38f, 0.5f);
            PlaceStaticCapsule(root, "LegL", hipL, footL, 0.13f, matSuit);
            PlaceStaticCapsule(root, "LegR", hipR, footR, 0.13f, matSuit);
        }

        private static void PlaceStaticCapsule(Transform root, string name, Vector3 a, Vector3 b, float diameter, Material mat)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            var cap = PrimitiveFactory.Capsule(name, root, (a + b) * 0.5f, diameter, len, mat, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            cap.transform.localScale = new Vector3(diameter, len * 0.5f, diameter);
        }
    }
}
