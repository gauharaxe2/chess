#if UNITY_EDITOR
using ChessMobile.Core;
using UnityEditor;
using UnityEngine;

namespace ChessMobile.EditorTools
{
    public static class ChessEngineTestMenu
    {
        [MenuItem("Chess Board/Run Engine Self-Test")]
        public static void Run()
        {
            var result = ChessEngineSelfTest.RunAll();
            EditorUtility.DisplayDialog(
                "Chess Engine Self-Test",
                $"{result.passed}/{result.total} checks passed.\nSee Console for failures.",
                "OK");
            Debug.Log($"[ChessEngineSelfTest] {result.passed}/{result.total} passed");
        }
    }
}
#endif
