using System;
using System.IO;
using System.Linq;
using MonkeyLab.Gameplay.Missions;
using MonkeyLab.Gameplay.Player;
using MonkeyLab.Presentation.Camera;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MonkeyLab.Tests.EditMode
{
    public sealed class ProjectFoundationTests
    {
        [Test]
        public void UnityVersion_IsPinnedTo6000_3()
        {
            StringAssert.StartsWith("6000.3.", Application.unityVersion);
        }

        [Test]
        public void RenderPipeline_IsConfigured()
        {
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null);
        }

        [Test]
        public void ReleaseScenes_ExistAndAreEnabled()
        {
            var expectedScenes = new[]
            {
                "00_Bootstrap.unity",
                "01_MainMenu.unity",
                "02_Lobby.unity",
                "10_Laboratory.unity"
            };

            var enabledSceneNames = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => Path.GetFileName(scene.path))
                .ToArray();

            CollectionAssert.AreEqual(expectedScenes, enabledSceneNames);
        }

        [Test]
        public void PlayerControls_ContainsFirstPlayableActions()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Project/Settings/PlayerControls.inputactions");

            Assert.That(actions, Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Move"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Look"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Interact"), Is.Not.Null);
            Assert.That(actions.FindAction("Gameplay/Flashlight"), Is.Not.Null);
        }

        [Test]
        public void LaboratoryScene_ContainsFirstPlayableComponents()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/10_Laboratory.unity");

            var player = GameObject.Find("P_Player_Local");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerInputReader>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerAimController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerInteractor>(), Is.Not.Null);
            Assert.That(Camera.main.GetComponent<QuarterViewCamera>(), Is.Not.Null);
            Assert.That(GameObject.Find("MissionStation_Fuse").GetComponent<FuseStationPrototype>(), Is.Not.Null);
            Assert.That(GameObject.Find("[Map] RoomWalls").transform.childCount, Is.GreaterThanOrEqualTo(20));

            Physics.SyncTransforms();
            var hasWalkableFloor = Physics.RaycastAll(
                    player.transform.position + Vector3.up * 2f,
                    Vector3.down,
                    4f,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Ignore)
                .Any(hit =>
                    hit.collider.gameObject.name.StartsWith("Room_", StringComparison.Ordinal) ||
                    hit.collider.gameObject.name.StartsWith("Corridor_", StringComparison.Ordinal));
            Assert.That(hasWalkableFloor, Is.True, "The local player must spawn above a walkable floor.");
        }
    }
}
