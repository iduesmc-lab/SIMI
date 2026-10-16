using System;
using DoctorSimiBoss.Core.Arena;
using UnityEngine;

namespace DoctorSimiBoss.Scene
{
    /// <summary>
    /// Physics layers from GlobalEnums.PhysLayers (checked in Assembly-CSharp 1.5.78.11833-77). Which pairs collide
    /// is game data, so the choice is made at runtime with Physics2D.GetIgnoreLayerCollision and logged.
    /// </summary>
    internal struct LayerChoice
    {
        public const int Default = 0;
        public const int Terrain = 8;
        public const int Player = 9;
        public const int Enemies = 11;
        public const int HeroDetector = 13;
        public const int HeroAttack = 17;
        public const int HeroBox = 20;
        public const int EnemyAttack = 22;

        public int EnemyBody;
        public int EnemyDamage;
        public int CameraLock;

        public static LayerChoice Pick(Action<string> log)
        {
            var choice = new LayerChoice
            {
                EnemyBody = Enemies,
                EnemyDamage = Collides(EnemyAttack, HeroBox) ? EnemyAttack : Enemies,
                CameraLock = Collides(HeroDetector, Player) ? HeroDetector : Default,
            };
            log("Layers: nail vs enemies " + (Collides(HeroAttack, Enemies) ? "collide" : "DO NOT collide") +
                "; damage layer " + choice.EnemyDamage + " vs hero box " + (Collides(choice.EnemyDamage, HeroBox) ? "collide" : "DO NOT collide") +
                "; camera lock layer " + choice.CameraLock + " vs player " + (Collides(choice.CameraLock, Player) ? "collide" : "DO NOT collide"));
            return choice;
        }

        private static bool Collides(int a, int b) { return !Physics2D.GetIgnoreLayerCollision(a, b); }
    }

    /// <summary>Builds the arena from the background image: sprite, invisible floor/walls/ceiling and a camera lock.</summary>
    internal static class ArenaBuilder
    {
        // CameraController.KeepWithinSceneBounds clamps the camera centre to x >= 14.6 and y >= 8.3,
        // i.e. half of the visible area at the gameplay plane.
        public const float CameraHalfWidth = 14.6f;
        public const float CameraHalfHeight = 8.3f;
        private const float Thickness = 2f;

        public static GameObject Build(ArenaLayout arena, Vector2 origin, Sprite background, LayerChoice layers)
        {
            var root = new GameObject("Doctor Simi Arena");
            root.transform.position = new Vector3(origin.x, origin.y, 0f);

            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            var renderer = bg.AddComponent<SpriteRenderer>();
            renderer.sprite = background;
            renderer.sortingOrder = -10;

            float left = arena.LeftWallX, right = arena.RightWallX, floor = arena.FloorY, top = arena.Height;
            AddSolid(root, "Floor", (left + right) / 2f, floor - Thickness / 2f, right - left + 2 * Thickness, Thickness);
            AddSolid(root, "Left Wall", left - Thickness / 2f, top / 2f, Thickness, top + 2 * Thickness);
            AddSolid(root, "Right Wall", right + Thickness / 2f, top / 2f, Thickness, top + 2 * Thickness);
            AddSolid(root, "Ceiling", (left + right) / 2f, top + Thickness / 2f, right - left + 2 * Thickness, Thickness);

            // The game's own camera lock: entering its trigger locks the camera to these bounds.
            var lockGo = new GameObject("Camera Lock");
            lockGo.layer = layers.CameraLock;
            lockGo.transform.SetParent(root.transform, false);
            var lockBox = lockGo.AddComponent<BoxCollider2D>(); // CameraLockArea.Awake reads this collider
            lockBox.isTrigger = true;
            lockBox.offset = new Vector2((left + right) / 2f, top / 2f);
            lockBox.size = new Vector2(right - left, top);
            var cameraLock = lockGo.AddComponent<CameraLockArea>();
            cameraLock.cameraXMin = origin.x + CameraHalfWidth;
            cameraLock.cameraXMax = origin.x + Math.Max(CameraHalfWidth, arena.Width - CameraHalfWidth);
            cameraLock.cameraYMin = origin.y + CameraHalfHeight;
            cameraLock.cameraYMax = origin.y + Math.Max(CameraHalfHeight, arena.Height - CameraHalfHeight);

            return root;
        }

        private static void AddSolid(GameObject root, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name);
            go.layer = LayerChoice.Terrain;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, height);
        }
    }
}
