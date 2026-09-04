using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Serialization;

public class MovableObjectAlgorithmService : MonoBehaviour
{
    [SerializeField] private List<MovableObjectAlgorithmGroup> _algorithmGroups;
    public IReadOnlyList<MovableObjectAlgorithmGroup> AlgorithmGroups => _algorithmGroups;

    private int _step = 0;
    // Monotonic counter incremented every time a step is completed. We wait on a change of
    // this counter instead of comparing _step values, because _step wraps back to 0 on the
    // last step — which made WaitUntil(_step > step) never complete for the final step,
    // so its OnStepEnd never fired and the unit froze on its last point.
    private int _stepCompletionCount = 0;
    private MovableObjectAlgorithmGroup _currentAlgorithmGroup;

    public async void StartCurrentAlgorithmStep()
    {
        if (_currentAlgorithmGroup == null)
        {
            Debug.LogError("Current algorithm group is null, please use ChangeAlgorithmGroup() instead.");
            return;
        }

        var group = _currentAlgorithmGroup;
        var step = _step;
        var completionToken = _stepCompletionCount;

        group.AlgorithmItems[step].OnStepStart?.Invoke();

        await UniTask.WaitUntil(() => _stepCompletionCount != completionToken);

        // The group may have been switched (e.g. clicking the unit -> WaitFireman, steal -> Stealing)
        // while we were awaiting. In that case this step belongs to an abandoned group and must NOT
        // fire its OnStepEnd, otherwise we'd index into the new (possibly smaller) group and crash.
        if (_currentAlgorithmGroup != group || step >= group.AlgorithmItems.Count)
            return;

        group.AlgorithmItems[step].OnStepEnd?.Invoke();
    }

    public void EndCurrentAlgorithmStep()
    {
        if (_currentAlgorithmGroup == null)
        {
            Debug.LogError("Current algorithm group is null, please use ChangeAlgorithmGroup() instead.");
            return;
        }

        // Signal completion of the current step regardless of index wrap-around.
        _stepCompletionCount++;

        if (_step + 1 >= _currentAlgorithmGroup.AlgorithmItems.Count)
        {
            _step = 0;
        }
        else
        {
            _step++;
        }
    }

    public void ChangeAlgorithmGroup(string algorithmName)
    {
        _step = 0;
        // Changing the group abandons any in-flight step awaits (see guard in StartCurrentAlgorithmStep).
        _stepCompletionCount++;
        _currentAlgorithmGroup = AlgorithmGroups.First(x => x.Name.Equals(algorithmName));
    }

    public void CallDebug(string message)
    {
        Debug.LogError(message);
    }
}
