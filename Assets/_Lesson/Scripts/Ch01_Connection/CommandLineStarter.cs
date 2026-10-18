using System;
using System.Collections;
using UnityEngine;

namespace Lesson
{
    /// <summary>
    /// [1장] 빌드한 실행 파일을 명령줄 옵션으로 바로 시작합니다. 버튼을 누르지 않아도 되므로
    /// "화면 없는 전용 서버(Dedicated Server)"를 띄우거나 여러 창을 한 번에 테스트할 때 씁니다.
    ///
    ///   UnityNetworkLesson.exe -mode server -port 7777 -batchmode -nographics
    ///   UnityNetworkLesson.exe -mode client -ip 127.0.0.1 -port 7777 -name 철수
    ///   -quitAfter 20   ← (선택) 20초 뒤 자동 종료. 자동 테스트용.
    /// </summary>
    [RequireComponent(typeof(ConnectionHud))]
    public class CommandLineStarter : MonoBehaviour
    {
        IEnumerator Start()
        {
            string mode = GetArg("-mode");
            if (mode == null) yield break; // 옵션이 없으면 평소처럼 버튼으로 시작

            // 한 프레임 기다려서 ConnectionHud.Start(콜백 등록)가 먼저 끝나게 합니다.
            yield return null;

            GetComponent<ConnectionHud>().StartAs(mode, GetArg("-ip"), GetArg("-port"), GetArg("-name"));

            if (float.TryParse(GetArg("-quitAfter"), out var seconds))
                Invoke(nameof(Quit), seconds);
        }

        void Quit()
        {
            ConnectionHud.Log("자동 종료");
            Application.Quit();
        }

        static string GetArg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
