using System;

namespace PrincesPalace.Domain.Preview
{
    // WHAT tools/preview.ps1 AND THE EDITOR SAY TO EACH OTHER, decided here.
    //
    // The transport is two files under Temp/ and the Editor half of it is
    // Editor/PreviewRequestWatcher.cs -- but almost nothing in that exchange
    // needs an editor. Which actions exist, which id each one requires, what a
    // result document carries, and the three state names are all decisions
    // over strings, and they were sitting inside an [InitializeOnLoad] class
    // that cannot be constructed outside a running Editor. So the protocol was
    // covered by nothing: the only way to find out that a result had stopped
    // echoing its requestId was for preview.ps1 to time out on it.
    //
    // The same split UiBindingContract makes, for the same reason: this half
    // owns the DOCUMENT, the Editor half owns the side effects (reading the
    // file, entering Play mode, rebuilding content). Serialization stays over
    // there too -- JsonUtility does the escaping, and a hand-rolled writer for
    // a message that can contain a resolver's exception text is a bug waiting
    // in a tool nobody watches.
    //
    // THE RULE THAT DECIDES THE SHAPE OF EVERY FUNCTION HERE: a result always
    // carries a requestId, because preview.ps1 IGNORES any result whose
    // requestId is not its own. A result that forgets to echo it is not a
    // failure the caller can read -- it is a caller that hangs until it times
    // out, which is what a missing echo has always looked like from the
    // terminal.

    // The request document, exactly as tools/preview.ps1 writes it.
    //
    // [Serializable] with public fields because Unity's JsonUtility parses it
    // on the other side; System.SerializableAttribute is BCL, so Domain stays
    // engine-free.
    [Serializable]
    public class PreviewRequest
    {
        // The GUID preview.ps1 generated for this one ask. Everything else is
        // optional; this is not.
        public string requestId;

        // "build" | "preview" | "spell" | "character". An unknown action is
        // reported by name rather than ignored -- a preview.ps1 from a newer
        // checkout talking to an older Editor should say so.
        public string action;

        public string enemyId;
        public string skillId;
        public string characterId;
        public string formation;
        public bool launch;
    }

    // The result document. Three fields, and the first of them is the one the
    // caller matches on.
    [Serializable]
    public class PreviewResult
    {
        public string requestId;
        public string state;
        public string message;
    }

    // Which of the four the request asked for.
    public enum PreviewAction
    {
        Unknown,
        Build,
        Preview,
        Spell,
        Character,
    }

    public static class PreviewProtocol
    {
        // Exactly one of these three, always. `busy` leaves the request file
        // alone (a compile ends in a domain reload that takes this Editor's
        // statics with it, and the file is the only thing that carries the ask
        // across); the other two consume it.
        public const string StateBusy = "busy";
        public const string StateOk = "ok";
        public const string StateFailed = "failed";

        public static readonly string[] States = { StateBusy, StateOk, StateFailed };

        // THE ONLY WAY A RESULT IS MADE. The requestId is coalesced rather
        // than trusted: a null one would serialize as an absent field and the
        // caller would read it as somebody else's result.
        public static PreviewResult Result(string requestId, string state, string message) =>
            new PreviewResult
            {
                requestId = requestId ?? "",
                state = state,
                message = message ?? "",
            };

        // A request with no requestId cannot be answered -- there is nothing
        // to echo, so whatever is written will be ignored by every caller,
        // including the one that sent it. The Editor half logs and discards it;
        // this exists so the reason is stated once and can be read in a test
        // rather than inferred from a Debug.LogWarning.
        public static bool IsAnswerable(PreviewRequest request) =>
            request != null && !string.IsNullOrEmpty(request.requestId);

        public static PreviewResult Unanswerable(PreviewRequest request) =>
            Result(request?.requestId, StateFailed,
                "the request file carries no requestId, so no result can be addressed to whoever wrote it; " +
                "discarded. This is preview.ps1 writing a malformed request, not a failure of the work it asked for.");

        public static PreviewAction ActionOf(string action)
        {
            switch (action)
            {
                case "build": return PreviewAction.Build;
                case "preview": return PreviewAction.Preview;
                case "spell": return PreviewAction.Spell;
                case "character": return PreviewAction.Character;
                default: return PreviewAction.Unknown;
            }
        }

        public static PreviewResult UnknownAction(PreviewRequest request) =>
            Result(request?.requestId, StateFailed,
                $"unknown action '{request?.action}' -- this Editor's PreviewRequestWatcher does not implement it");

        // Which field carries the thing to act on, per action. `build` needs
        // none: it rebuilds the whole catalogue.
        public static string TargetFieldOf(PreviewAction action)
        {
            switch (action)
            {
                case PreviewAction.Preview: return "enemyId";
                case PreviewAction.Spell: return "skillId";
                case PreviewAction.Character: return "characterId";
                default: return null;
            }
        }

        public static string TargetIdOf(PreviewRequest request, PreviewAction action)
        {
            if (request == null) return null;

            switch (action)
            {
                case PreviewAction.Preview: return request.enemyId;
                case PreviewAction.Spell: return request.skillId;
                case PreviewAction.Character: return request.characterId;
                default: return null;
            }
        }

        public static PreviewResult MissingTarget(PreviewRequest request, PreviewAction action) =>
            Result(request?.requestId, StateFailed,
                $"no {TargetFieldOf(action)} in the request");

        public static PreviewResult Busy(PreviewRequest request) =>
            Result(request?.requestId, StateBusy,
                "the Editor is compiling or importing; waiting for it to settle");

        public static PreviewResult InPlayMode(PreviewRequest request) =>
            Result(request?.requestId, StateFailed,
                "the Editor is in Play mode -- exit Play mode first, then re-run preview.ps1");

        // SCREENING, in the order the watcher applies it: is there an id to
        // answer, is the action one we implement, does that action have the
        // field it needs. Returns null when the request is fit to act on.
        //
        // The busy and Play-mode checks are NOT here: both are questions about
        // the Editor rather than about the document, and they sit between
        // these two halves in the watcher (busy before the action is looked at,
        // because a compile invalidates nothing about the request; Play mode
        // before the work, because the work is what cannot run).
        public static PreviewResult Screen(PreviewRequest request)
        {
            if (!IsAnswerable(request)) return Unanswerable(request);

            var action = ActionOf(request.action);
            if (action == PreviewAction.Unknown) return UnknownAction(request);

            if (TargetFieldOf(action) != null && string.IsNullOrEmpty(TargetIdOf(request, action)))
            {
                return MissingTarget(request, action);
            }

            return null;
        }
    }
}
