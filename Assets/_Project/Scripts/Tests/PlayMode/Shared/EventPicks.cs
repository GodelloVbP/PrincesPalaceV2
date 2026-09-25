using PrincesPalace.Domain.Events;

namespace PrincesPalace.PlayModeTests
{
    // A SCRIPTED PICK ON THE PAGE THE RUN IS ON RIGHT NOW. RunOrchestrator
    // .ChooseEventOption takes the page the caller painted, and refuses a
    // stale one (EventRefusal.StalePage). A test that reads the run and picks
    // in one synchronous step has painted exactly the current page, so this
    // passes it. A test OF the stale-page guard calls ChooseEventOption with
    // the page it captured itself (EventStalePageTests).
    internal static class EventPicks
    {
        internal static EventChoiceResult OnCurrentPage(int index) =>
            RunOrchestrator.ChooseEventOption(index, RunManager.Run?.eventPageId);
    }
}
