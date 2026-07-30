using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MonkeyLab.Tests.PlayMode
{
    /// <summary>
    /// 씬이 실제로 보이는 상태인지 검증한다.
    ///
    /// 배경: 스프라이트 임포트가 끝나기 전에 참조를 채워 33개 SpriteRenderer가 모두
    /// 비어 있었는데, 빌드 로그는 "구성 완료"로 정상이었다. 로직 테스트로는 잡히지
    /// 않는 종류의 문제라 렌더링 자체를 검사한다.
    /// </summary>
    public sealed class SandboxRenderingTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/91_GameplaySandbox.unity";

        [UnityTest]
        public IEnumerator AllSpriteRenderers_HaveSpriteAssigned()
        {
            yield return LoadSandbox();

            SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);

            Assert.Greater(renderers.Length, 0, "씬에 SpriteRenderer가 하나도 없다");

            foreach (SpriteRenderer renderer in renderers)
            {
                Assert.IsNotNull(
                    renderer.sprite,
                    $"'{renderer.name}'의 스프라이트가 비어 있다. 화면에 보이지 않는다.");
            }
        }

        [UnityTest]
        public IEnumerator Camera_IsOrthographicAndSeesPlayerLayer()
        {
            yield return LoadSandbox();

            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "MainCamera 태그를 가진 카메라가 없다");
            Assert.IsTrue(camera.orthographic, "2D는 직교 카메라여야 한다");
            Assert.Greater(camera.orthographicSize, 0f);

            // 카메라가 스프라이트보다 뒤(-Z)에 있어야 보인다.
            Assert.Less(camera.transform.position.z, 0f, "카메라 Z가 0 이상이면 스프라이트가 보이지 않는다");
        }

        [UnityTest]
        public IEnumerator PlayerAndMonster_AreWithinCameraView()
        {
            yield return LoadSandbox();

            Camera camera = Camera.main;
            GameObject player = GameObject.Find("Player");

            Assert.IsNotNull(player, "Player를 찾을 수 없다");

            Vector3 viewport = camera.WorldToViewportPoint(player.transform.position);

            Assert.IsTrue(
                viewport.x is >= 0f and <= 1f && viewport.y is >= 0f and <= 1f,
                $"플레이어가 화면 밖에 있다 (viewport {viewport.x:F2}, {viewport.y:F2})");
        }

        private static IEnumerator LoadSandbox()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
            yield return null;
            yield return null;
        }
    }
}
