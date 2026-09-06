using System;
using System.IO;
using PrincesPalace;
using PrincesPalace.Domain.Preview;
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
// THE DOCUMENTS THEMSELVES LIVE IN Domain/Preview/PreviewProtocol.cs. What
// stays here is everything that needs an editor: the poll, the two files, the
// two questions about Editor state (compiling, playing), and the four pieces
// of work. Which actions exist, which id each needs, what a result carries and
// the three state names are decisions over strings, and they were untestable
// while they sat inside this [InitializeOnLoad] class.
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

        PreviewRequest request;
        try
        {
            request = JsonUtility.FromJson<PreviewRequest>(File.ReadAllText(RequestFile));
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

        if (!PreviewProtocol.IsAnswerable(request))
        {
            // Not written out as a result file: with no requestId to echo,
            // every caller including the one that sent it would ignore it.
            // PreviewProtocol.Unanswerable says the same thing where a test can
            // read it.
            Debug.LogWarning("[PreviewRequestWatcher] " + PreviewProtocol.Unanswerable(request).message);
            TryDelete(RequestFile);
            return;
        }

        // BUSY FIRST, and the request survives. See the header: the reload at
        // the end of a compile takes this class's statics with it, and the
        // request file is the only thing that carries the ask across.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Write(PreviewProtocol.Busy(request));
            return;
        }

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            TryDelete(RequestFile);
            Write(PreviewProtocol.InPlayMode(request));
            return;
        }

        // CONSUMED BEFORE THE WORK, not after. Content generation can throw,
        // and a request still on disk when it does would be retried on the
        // next tick, forever, against the same broken input.
        TryDelete(RequestFile);
        Handle(request);
    }

    // ROUTING ONLY. Everything refusable about the document itself -- an
    // action this Editor does not implement, an action whose id is missing --
    // is decided by PreviewProtocol.Screen, so the four methods below start
    // from a request that is already known to be actionable.
    private static void Handle(PreviewRequest request)
    {
        var refusal = PreviewProtocol.Screen(request);
        if (refusal != null)
        {
            Write(refusal);
            return;
        }

        switch (PreviewProtocol.ActionOf(request.action))
        {
            case PreviewAction.Build:
                Build(request);
                return;
            case PreviewAction.Preview:
                Preview(request);
                return;
            case PreviewAction.Spell:
                Spell(request);
                return;
            case PreviewAction.Character:
                CharacterPreview(request);
                return;
        }
    }

    private static void Build(PreviewRequest request)
    {
        try
        {
            ContentBuilder.BuildDefaultContent();
            WriteResult(request.requestId, PreviewProtocol.StateOk, "content rebuilt in the open Editor");
        }
        catch (Exception e)
        {
            // The message, not the stack: preview.ps1 prints this straight to
            // the author, and a resolver error already names the offending
            // entry. The stack is in the Editor console for whoever needs it.
            Debug.LogException(e);
            WriteResult(request.requestId, PreviewProtocol.StateFailed, e.Message);
        }
    }

    // Opens the Fight scene against one named mob and enters Play.
    //
    // THE RESULT IS WRITTEN BEFORE PLAY STARTS, and that ordering is not
    // cosmetic: entering Play mode runs a domain reload, and everything static
    // in this class -- including the fact that a request was being handled --
    // is gone on the other side of it. A result written afterwards would be
    // written by nobody.
    private static void Preview(PreviewRequest request)
    {
        string id = request.enemyId;

        try
        {
            // Remembered before the fight, not after, so a mob that crashes the
            // scene is still the one the menu item offers to retry.
            QuickFightMenu.LastPreviewedEnemyId = id;

            FightBootstrap.DevForcedFormation = request.formation;
            FightBootstrap.DevForcedEnemyScript = true;

            WriteResult(request.requestId, PreviewProtocol.StateOk,
                $"entering Play mode against '{id}' ({(request.formation == PreviewFight.FormationFull ? "full formation" : "lone")}), " +
                "showcasing its abilities in authored order");

            QuickFightMenu.StartPlaceholderFight(id);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            WriteResult(request.requestId, PreviewProtocol.StateFailed, e.Message);
        }
    }

    // tools/preview.ps1 -Spell <id> -Launch.
    //
    // THE PLAN IS MADE BEFORE PLAY, in this Editor, so a refusal ("no
    // character in content carries resource X", "Ward is not something the
    // preview can stand a fight up for") reaches preview.ps1 as a `failed`
    // result the author reads on their own terminal -- rather than as a
    // warning buried in a console they would have to go and open, behind a
    // Play mode they did not want to enter. PreviewFight.ForSpell touches
    // nothing; it only answers.
    private static void Spell(PreviewRequest request)
    {
        string id = request.skillId;

        try
        {
            var plan = PreviewFight.ForSpell(id);
            if (!plan.Ok)
            {
                WriteResult(request.requestId, PreviewProtocol.StateFailed, plan.Refusal);
                return;
            }

            FightBootstrap.DevForcedSkillId = id;
            FightBootstrap.DevForcedFirstAction = id;
            FightBootstrap.DevForcedFormation = plan.Formation;

            WriteResult(request.requestId, PreviewProtocol.StateOk, "entering Play mode: " + PreviewFight.Describe(plan));

            // No enemy id: the spell preview is about the caster, and the
            // placeholder's own art-filtered pick is a perfectly good thing to
            // aim at. See QuickFightMenu's own note on the empty id.
            QuickFightMenu.StartPlaceholderFight(null);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            WriteResult(request.requestId, PreviewProtocol.StateFailed, e.Message);
        }
    }

    // tools/preview.ps1 -Character <id> -Launch.
    //
    // The squad is this one character -- DevForcedSquad, consumed by
    // FightBootstrap like every other preview key -- and turn one casts
    // whatever their own kit puts first, through the same ForceFirstAction
    // seam the spell route uses. Nothing is granted here: the opener is a row
    // they already have, so what is on screen is the character as authored.
    private static void CharacterPreview(PreviewRequest request)
    {
        string id = request.characterId;

        try
        {
            var plan = PreviewFight.ForCharacter(id);
            if (!plan.Ok)
            {
                WriteResult(request.requestId, PreviewProtocol.StateFailed, plan.Refusal);
                return;
            }

            FightBootstrap.DevForcedSquad = id;
            if (plan.Skill != null) FightBootstrap.DevForcedFirstAction = plan.Skill.Id;

            WriteResult(request.requestId, PreviewProtocol.StateOk, "entering Play mode: " + PreviewFight.Describe(plan));

            QuickFightMenu.StartPlaceholderFight(null);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            WriteResult(request.requestId, PreviewProtocol.StateFailed, e.Message);
        }
    }

    // Temp-then-replace, so a reader polling this file every 300ms can never
    // catch it half written. File.Replace rather than Move because Move onto
    // an existing path throws on Windows.
    private static void WriteResult(string requestId, string state, string message) =>
        Write(PreviewProtocol.Result(requestId, state, message));

    private static void Write(PreviewResult result)
    {
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
