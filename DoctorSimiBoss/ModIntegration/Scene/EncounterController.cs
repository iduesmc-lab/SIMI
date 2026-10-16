using System;
using DoctorSimiBoss.Core;
using DoctorSimiBoss.Core.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// Debug access to the fight. Pressing the key in a gameplay room builds the arena to the right of
    /// the current room (outside its bounds, so nothing in the room is touched), moves the Knight in and
    /// spawns the boss. Pressing it again removes everything and returns the Knight to where it was.
    /// Leaving the scene any other way (death, quitting) destroys the arena with the scene.
    /// </summary>
    internal sealed class EncounterController : MonoBehaviour
    {
        private const float GapAfterRoom = 40f;

        private BossDefinition _definition;
        private SpriteLibrary _sprites;
        private ArenaLayout _arena;
        private KeyCode _key;
        private Action<string> _log;

        private GameObject _arenaRoot;
        private DoctorSimiBody _boss;
        private Vector3 _returnPosition;
        private float _savedXLimit, _savedYLimit;

        public static void Install(BossDefinition definition, SpriteLibrary sprites, KeyCode key, Action<string> log)
        {
            var go = new GameObject("DoctorSimiBoss Encounter");
            DontDestroyOnLoad(go);
            var controller = go.AddComponent<EncounterController>();
            controller._definition = definition;
            controller._sprites = sprites;
            controller._arena = definition.CreateArenaLayout();
            controller._key = key;
            controller._log = log;
            UnitySceneManager.activeSceneChanged += controller.OnActiveSceneChanged;
            HitEffectsDonor.TryCapture(log);
        }

        private void OnDestroy()
        {
            UnitySceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
        {
            HitEffectsDonor.TryCapture(_log);
            if (_arenaRoot == null && _boss == null) return;
            // The arena belonged to the old scene and was destroyed with it; the camera re-initialises its limits.
            _arenaRoot = null;
            _boss = null;
            BossMusic.Forget();
            _log("Scene changed; arena closed.");
        }

        private void Update()
        {
            if (!Input.GetKeyDown(_key)) return;
            try
            {
                if (_arenaRoot != null) Leave();
                else Enter();
            }
            catch (Exception e)
            {
                _log("Encounter error: " + e);
            }
        }

        private void Enter()
        {
            var gm = GameManager.instance;
            var hero = HeroController.instance;
            if (gm == null || hero == null || !gm.IsGameplayScene() || gm.isPaused ||
                hero.cState.dead || hero.cState.transitioning || !hero.acceptingInput)
            {
                _log("Arena not opened: needs a gameplay room, not paused, with the Knight in control.");
                return;
            }

            var cameraCtrl = GameCameras.instance.cameraController;
            float roomWidth = cameraCtrl.sceneWidth > 0 ? cameraCtrl.sceneWidth : hero.transform.position.x + 100f;
            var origin = new Vector2(roomWidth + GapAfterRoom, 0f);

            HitEffectsDonor.TryCapture(_log);
            var layers = LayerChoice.Pick(_log);
            _savedXLimit = cameraCtrl.xLimit;
            _savedYLimit = cameraCtrl.yLimit;
            cameraCtrl.xLimit = Math.Max(_savedXLimit, origin.x + _arena.Width - ArenaBuilder.CameraHalfWidth);
            cameraCtrl.yLimit = Math.Max(_savedYLimit, origin.y + _arena.Height - ArenaBuilder.CameraHalfHeight);

            _arenaRoot = ArenaBuilder.Build(_arena, origin, _sprites.Background, layers);
            _boss = DoctorSimiBody.Create(_definition, _sprites, _arena, origin, _arenaRoot.transform, layers, _log);
            _boss.Defeated += () => BossMusic.Restore(_log, 3f);
            var presentation = _definition.Manifest.Presentation;
            if (_definition.Manifest.Arena.Rain) RainFx.Create(_arenaRoot.transform, _arena.Width, _arena.Height);
            BossMusic.Play(presentation, _log);
            BossTitle.Show(_arenaRoot.transform, presentation.Title, _definition.Tuning.IntroDuration + 0.3f, _log);

            _returnPosition = hero.transform.position;
            MoveHero(hero, new Vector3(origin.x + _arena.PlayerEntryX, origin.y + _arena.FloorY + 1.5f, _returnPosition.z));
            cameraCtrl.SnapTo(origin.x + ArenaBuilder.CameraHalfWidth, origin.y + ArenaBuilder.CameraHalfHeight);

            _log("Arena opened in " + gm.GetSceneNameString() + " at x=" + origin.x.ToString("0.#") +
                 ". Press " + _key + " again to leave.");
        }

        private void Leave()
        {
            var hero = HeroController.instance;
            var cameraCtrl = GameCameras.instance.cameraController;

            Destroy(_arenaRoot);
            _arenaRoot = null;
            BossMusic.Restore(_log);
            _boss = null;
            cameraCtrl.xLimit = _savedXLimit;
            cameraCtrl.yLimit = _savedYLimit;

            if (hero != null)
            {
                MoveHero(hero, _returnPosition);
                cameraCtrl.SnapTo(_returnPosition.x, _returnPosition.y);
            }
            _log("Arena closed.");
        }

        private static void MoveHero(HeroController hero, Vector3 position)
        {
            hero.transform.position = position;
            var rb = hero.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }
}
