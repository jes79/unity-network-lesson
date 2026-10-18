using System;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// 키보드 입력을 한곳에서 읽습니다.
    /// 실행 옵션에 -bot 을 주면 사람 대신 "원을 그리며 걷고, 가끔 쏘는" 자동 입력을 돌려줍니다.
    /// (손을 대지 않고 여러 창의 동기화를 관찰하거나 자동 테스트할 때 사용)
    /// </summary>
    public static class LessonInput
    {
        public static readonly bool IsBot = Array.IndexOf(Environment.GetCommandLineArgs(), "-bot") >= 0;

        /// <summary>채팅 입력 중이면 true (3장). 이때는 키보드로 캐릭터를 조작하지 않는다.</summary>
        public static bool BlockKeyboard;

        /// <summary>WASD / 방향키. x = 좌우, y = 앞뒤.</summary>
        public static Vector2 Move()
        {
            if (IsBot)
            {
                float t = Time.time * 0.8f;
                return new Vector2(Mathf.Cos(t), Mathf.Sin(t));
            }
            if (BlockKeyboard) return Vector2.zero;
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        /// <summary>스페이스바 (3장: 발사)</summary>
        public static bool Fire()
        {
            if (IsBot) return Time.frameCount % 90 == 0;
            return !BlockKeyboard && Input.GetKeyDown(KeyCode.Space);
        }
    }
}
