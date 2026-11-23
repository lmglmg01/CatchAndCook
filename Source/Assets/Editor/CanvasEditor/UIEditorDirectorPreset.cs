using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Effect = UIDirectorView.Effect;

/// <summary>대상·View·함수 참조 없이 조합한 연출 설정만 보관하는 편집기 전용 에셋입니다.</summary>
public sealed class UIEditorDirectorPreset : ScriptableObject
{
    public int version = 1;
    public string displayName;
    public bool allTracks;
    public string sourceTrack;
    public bool loopYoyo = true;
    public List<Effect> effects = new(), enter = new(), exit = new(), loop = new(), reaction = new();

    /// <summary>불러온 효과를 수정해도 저장 에셋과 다른 대상에 영향을 주지 않도록 깊은 복사합니다.</summary>
    public static List<Effect> Copy(IEnumerable<Effect> source) =>
        (source ?? Enumerable.Empty<Effect>()).Where(e => e != null)
        .Select(e => JsonUtility.FromJson<Effect>(JsonUtility.ToJson(e))).ToList();

    /// <summary>현재 효과가 끝난 뒤 추가 조합의 상대적인 실행 순서를 유지하여 연결합니다.</summary>
    public static void Append(List<Effect> destination, IEnumerable<Effect> source)
    {
        var copy = Copy(source);
        float previousStart = 0, previousEnd = 0, end = 0;
        foreach (var effect in destination.Where(e => e != null && e.enabled))
        {
            previousStart = UIDirectorView.Start(effect, previousStart, previousEnd);
            previousEnd = previousStart + Mathf.Max(.01f, effect.duration);
            end = Mathf.Max(end, previousEnd);
        }
        bool first = true;
        foreach (var effect in copy)
        {
            if (!effect.enabled) continue;
            if (first || effect.startMode == UIDirectorView.StartMode.First)
            {
                effect.startMode = UIDirectorView.StartMode.First;
                effect.delay += end;
            }
            first = false;
        }
        destination.AddRange(copy);
    }
}
