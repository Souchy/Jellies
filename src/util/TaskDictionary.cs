using Souchy.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellies.src.util;

public class TaskDictionary
{
    public IntIdSync Ids { get; } = new();
    public Dictionary<int, TaskCompletionSource> Tasks { get; } = new();

    public (int id, TaskCompletionSource tcs) GetTask()
    {
        int key = Ids.GetNextId();
        Tasks[key] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return (key, Tasks[key]);
    }

    public void Complete(int key)
    {
        if (Tasks.TryGetValue(key, out var tcs))
        {
            Tasks.Remove(key);
            tcs.SetResult();
            Ids.ReleaseId(key);
        }
    }

}
