using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace Lightsale.Utility
{
    public delegate void RunAfterCallback();

    public static class Routines
    {
        public static void CompleteExecution(IEnumerator routine)
        {
            // This consumes all of the enumerator's yields and discards
            // the results, which for a coroutine are null. Don't use w/
            // non-terminating routines.
            while (routine.MoveNext()) ;
        }

        public static IEnumerator RunAfter(IEnumerator routine, RunAfterCallback callback)
        {
            yield return routine;
            callback();
        }

        public static void CheckedRoutine(this MonoBehaviour owner, ref Coroutine id, IEnumerator routine)
        {
            if (id != null)
                owner.StopCoroutine(id);
            id = owner.StartCoroutine(routine);
        }

        public static IEnumerator Serial(params IEnumerator[] routines)
        {
            foreach (IEnumerator routine in routines)
            {
                yield return routine;
            }
        }

        public static IEnumerator Serial(UnityEvent onFinish, params IEnumerator[] routines)
        {
            foreach (IEnumerator routine in routines)
            {
                yield return routine;
            }
            if (onFinish != null)
            {
                onFinish.Invoke();
            }
        }
    }
}