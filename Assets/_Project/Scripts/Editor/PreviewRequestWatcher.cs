using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// THE EDITOR-OPEN HALF OF tools/preview.ps1.
//
// Unity takes an exclusive lock on a project's Library, so batchmode cannot
// run against a project whose Editor is open -- and the Editor is open most of
// the day. tools/build_content.ps1 is the route for a closed Editor; this is
// the route for an open one. Same work, different door: preview.ps1 picks by
// looking for Temp\UnityLockfile and never asks the author which they are on.
//
// THE PROTOCOL, in full, because both halves have to agree on it exactly:
//
//   * preview.ps1 writes Temp/pp_request.<guid>.json carrying that same GUID
//     as `requestId`, then MOVES it onto Temp/pp_request.json without
//     overwriting. A second concurrent request therefore fails at the move,
//     loudly, in the caller -- rather than quietly replacing a request this
//     Editor is halfway through.
//   * This watcher writes Temp/pp_result.tmp and then replaces
//     Temp/pp_result.json with it, so a reader never sees a half-written
//     file. Every result echoes `requestId` and carries `state` of exactly
//     one of busy | ok | failed, plus a human `message`.
//   * preview.ps1 IGNORES a result whose requestId is not its own. That is
//     what makes a stale result from a previous session harmless.
//   * COMPILING OR RELOADING: state `busy`, and THE REQUEST FILE IS LEFT
//     ALONE. A script compile ends in a domain reload, which throws away
//     every static field in this class -- [InitializeOnLoad] re-arms the
//     watcher on the other side and it finds the same request still sitting
//     there. Deleting the request here would lose it across exactly the
//     reload it was reporting.
//   * ALREADY IN PLAY MODE: state `failed`, "exit Play mode first". The
//     watcher never stops Play itself. An author who left a fight running has
//     unsaved state in front of them and a tool that yanks it is a tool they
//     stop running.
//
// STATE IS PREVIEW-OWNED. The only thing this writes outside Temp/ is
// SessionState, which is Editor-process-local and dies with the Editor. It
// never touches SaveSystem, never writes a save slot, and the fight it asks
// for is FightBootstrap's placeholder (RunManager.HasRun false), so the
// player's real run and profile are neither read for the squad nor written.
[InitializeOnLoad]
public static class PreviewRequestWatcher
{
    // Relative to the project root, which is the Editor's working directory.
    // Temp/ is Unity's own scratch folder: gitignored, and wiped when the
    // Editor closes, so nothing here can outlive the session that wrote it.
    private const string RequestFile = "Temp/pp_request.json";
    private const string ResultFile = "Temp/pp_result.json";
    private const string ResultTemp = "Temp/pp_result.tmp";

    // Every update tick is ~100 polls a second for a file that appears a few
    // times an hour. Half a second is imperceptible to the author waiting on
    // it and invisible in the profiler.
    private const double PollSeconds = 0.5;

    private static double _nextPoll;

    static PreviewRequestWatcher()
    {
        // BATCHMODE IS NOT A ROUTE. build_content.ps1 already owns the closed-
        // Editor case, and a batchmode run that happened to find a request file
        // left over in Temp/ would act on it in the middle of somebody's test
        // run. EditorApplication.update barely ticks under -executeMethod
        // anyway, so this guard is about intent as much as behaviour.
        if (Application.isBatchMode)
        {
            return;
        }

        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [Serializable]
    private class Request
    {
        public string requestId;

        // What to do. "build" regenerates the content assets in this Editor;
        // 1c adds the preview actions. An unknown action is `failed` with the
        // name in the message rather than ignored -- a preview.ps1 from a
        // newer checkout talking to an older Editor should say so.
        public string action;
        public string enemyId;
        public string formation;
        public bool launch;
    }

    [Serializable]
    private class Result
    {
        public string requestId;
        public string state;
        public string message;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _nextPoll)
        {
            return;
        }

        _nextPoll = EditorApplication.timeSinceStartup + PollSeconds;

        if (!File.Exists(RequestFile))
        {
            return;
        }

        Request request;
        try
        {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestFile));
        }
        catch (Exception e)
        {
            // A request we cannot even parse has no requestId to echo, so the
            // caller will time out rather than read this -- but the Editor
            // console is where a human will look, and the file has to go or
            // every tick will retry it forever.
            Debug.LogWarning($"[PreviewRequestWatcher] unreadable request file, discarding it: {e.Message}");
            TryDelete(RequestFile);
            return;
        }

        if (request == null || string.IsNullOrEmpty(request.requestId))
        {
            Debug.LogWarning("[PreviewRequestWatcher] request file carries no requestId, discarding it.");
            TryDelete(RequestFile);
            return;
        }

        // BUSY FIRST, and the request survives. See the header: the reload at
        // the end of a compile takes this class's statics with it, and the
        // request file is the only thing that carries the ask across.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            WriteResult(request.requestId, "busy", "the Editor is compiling or importing; waiting for it to settle");
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            TryDelete(RequestFile);
            WriteResult(request.requestId, "failed", "the Editor is in Play mode -- exit Play mode first, then re-run preview.ps1");
            return;
        }

        // CONSUMED BEFORE THE WORK, not after. Content generation can throw,
        // and a request still on disk when it does would be retried on the
        // next tick, forever, against the same broken input.
        TryDelete(RequestFile);
        Handle(request);
    }

    private static void Handle(Request request)
    {
        switch (request.action)
        {
            case "build":
                Build(request);
                return;
            default:
                WriteResult(request.requestId, "failed",
                    $"unknown action '{request.action}' -- this Editor's PreviewRequestWatcher does not implement it");
                return;
        }
    }

    private static void Build(Request request)
    {
        try
        {
            ContentBuilder.BuildDefaultContent();
            WriteResult(request.requestId, "ok", "content rebuilt in the open Editor");
        }
        catch (Exception e)
        {
            // The message, not the stack: preview.ps1 prints this straight to
            // the author, and a resolver error already names the offending
            // entry. The stack is in the Editor console for whoever needs it.
            Debug.LogException(e);
            WriteResult(request.requestId, "failed", e.Message);
        }
    }

    // Temp-then-replace, so a reader polling this file every 300ms can never
    // catch it half written. File.Replace rather than Move because Move onto
    // an existing path throws on Windows.
    private static void WriteResult(string requestId, string state, string message)
    {
        var result = new Result { requestId = requestId, state = state, message = message };

        try
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ResultTemp, JsonUtility.ToJson(result, true));

            if (File.Exists(ResultFile))
            {
                File.Replace(ResultTemp, ResultFile, null);
            }
            else
            {
                File.Move(ResultTemp, ResultFile);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PreviewRequestWatcher] could not write {ResultFile}: {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PreviewRequestWatcher] could not delete {path}: {e.Message}");
        }
    }
}
