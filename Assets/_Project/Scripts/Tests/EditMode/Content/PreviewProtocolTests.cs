using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Preview;

namespace PrincesPalace.Domain.Tests
{
    // THE REQUEST/RESULT PROTOCOL tools/preview.ps1 and the open Editor share.
    //
    // None of this was covered, because all of it lived inside an
    // [InitializeOnLoad] Editor class that cannot be constructed without a
    // running Editor. The failure mode that made it worth extracting: a result
    // that stops echoing its requestId is not a failure preview.ps1 can read
    // -- the caller IGNORES any result whose id is not its own, so a dropped
    // echo looks exactly like an Editor that never answered, and the author
    // sees a timeout naming nothing.
    //
    // What stays in Editor/PreviewRequestWatcher.cs is what needs an editor:
    // the poll, the two files under Temp/, the two questions about Editor
    // state, and the four pieces of work. Neither of those two questions is
    // covered here or anywhere -- `busy` fires only when a compile lands inside
    // the poll window, and the Play-mode refusal needs an Editor already in
    // Play. Both were left unexercised on 2026-09-06 because no Editor was
    // open on this project (Temp/UnityLockfile was present with no Unity.exe
    // behind it, which build_content.ps1 recognises as debris). Said out loud
    // rather than left as an absence: what is covered below is the document,
    // not the two moments the watcher refuses to act on one.
    public class PreviewProtocolTests
    {
        private const string Id = "5b0e2f1c-preview-request";

        private static PreviewRequest Request(string action, string enemyId = null,
                                              string skillId = null, string characterId = null,
                                              string requestId = Id, string element = null) =>
            new PreviewRequest
            {
                requestId = requestId,
                action = action,
                enemyId = enemyId,
                skillId = skillId,
                characterId = characterId,
                element = element,
            };

        // ---- the echo --------------------------------------------------------

        // EVERY result, from every constructor there is. Written as a sweep
        // rather than one assertion per message because the rule is about the
        // shape of the document and not about any one of them -- a new result
        // added without an echo is exactly what this has to catch.
        private static IEnumerable<PreviewResult> EveryKindOfResult(PreviewRequest request) =>
            new[]
            {
                PreviewProtocol.Result(request.requestId, PreviewProtocol.StateOk, "done"),
                PreviewProtocol.Unanswerable(request),
                PreviewProtocol.UnknownAction(request),
                PreviewProtocol.MissingTarget(request, PreviewAction.Preview),
                PreviewProtocol.Busy(request),
                PreviewProtocol.InPlayMode(request),
            };

        [Test]
        public void EveryResultEchoesTheRequestId()
        {
            foreach (var result in EveryKindOfResult(Request("preview")))
            {
                Assert.AreEqual(Id, result.requestId,
                    "preview.ps1 ignores a result whose requestId is not its own, so a result that does not " +
                    "echo it is a caller that hangs until it times out.");
            }
        }

        [Test]
        public void EveryResultCarriesOneOfTheThreeStateNames()
        {
            foreach (var result in EveryKindOfResult(Request("preview")))
            {
                CollectionAssert.Contains(PreviewProtocol.States, result.state);
                Assert.IsNotEmpty(result.message ?? "", "a state with no message tells the author nothing.");
            }
        }

        [Test]
        public void TheThreeStateNamesAreTheOnesTheProtocolDocuments()
        {
            CollectionAssert.AreEqual(new[] { "busy", "ok", "failed" }, PreviewProtocol.States);
        }

        // A null id would serialize as an absent field, which reads to the
        // caller as somebody else's result rather than as a malformed one.
        [Test]
        public void AResultNeverCarriesANullId()
        {
            var result = PreviewProtocol.Result(null, PreviewProtocol.StateFailed, "boom");

            Assert.AreEqual("", result.requestId);
        }

        // ---- what is refused -------------------------------------------------

        [Test]
        public void ARequestWithNoIdIsFailedAndEchoesNothing()
        {
            foreach (var request in new[] { Request("build", requestId: null), Request("build", requestId: "") })
            {
                Assert.IsFalse(PreviewProtocol.IsAnswerable(request));

                var refusal = PreviewProtocol.Screen(request);

                Assert.IsNotNull(refusal);
                Assert.AreEqual(PreviewProtocol.StateFailed, refusal.state);
                Assert.AreEqual("", refusal.requestId);
                StringAssert.Contains("no requestId", refusal.message);
            }
        }

        [Test]
        public void AnUnknownActionIsFailedAndNamesIt()
        {
            var refusal = PreviewProtocol.Screen(Request("photograph"));

            Assert.IsNotNull(refusal);
            Assert.AreEqual(PreviewProtocol.StateFailed, refusal.state);
            Assert.AreEqual(Id, refusal.requestId);
            StringAssert.Contains("photograph", refusal.message);
            StringAssert.Contains("does not implement", refusal.message);
        }

        // An action that needs an id and was not given one names the FIELD,
        // because that is what the author has to add.
        [TestCase("preview", "enemyId")]
        [TestCase("spell", "skillId")]
        [TestCase("character", "characterId")]
        public void AnActionWithoutItsIdNamesTheFieldItWanted(string action, string field)
        {
            var refusal = PreviewProtocol.Screen(Request(action));

            Assert.IsNotNull(refusal, $"'{action}' with no {field} should be refused.");
            Assert.AreEqual(PreviewProtocol.StateFailed, refusal.state);
            StringAssert.Contains(field, refusal.message);
        }

        // ---- and what is not -------------------------------------------------

        [Test]
        public void BuildNeedsNoId()
        {
            Assert.IsNull(PreviewProtocol.Screen(Request("build")));
            Assert.IsNull(PreviewProtocol.TargetFieldOf(PreviewAction.Build));
        }

        [TestCase("preview")]
        [TestCase("spell")]
        [TestCase("character")]
        public void AWellFormedRequestIsNotRefused(string action)
        {
            var request = Request(action, enemyId: "rat", skillId: "shear", characterId: "sheep");

            Assert.IsNull(PreviewProtocol.Screen(request));
            Assert.AreNotEqual(PreviewAction.Unknown, PreviewProtocol.ActionOf(action));
            Assert.IsNotEmpty(PreviewProtocol.TargetIdOf(request, PreviewProtocol.ActionOf(action)));
        }

        // ---- the optional half of a spell request ----------------------------
        //
        // -Element NARROWS a request rather than selecting one, which is why it
        // is the only field on the document that no rule requires. Both halves
        // of that are pinned: a spell request without it is not refused, and a
        // spell request with it does not become some other action.
        //
        // WHAT IS DELIBERATELY NOT CHECKED HERE: whether the element is one the
        // skill offers. That needs a content database, which this half of the
        // protocol has neither. PreviewFight.ElementRefusal owns it and
        // PreviewFightRefusalTests pins the message.
        [Test]
        public void ASpellRequestIsNotRefusedForCarryingNoElement()
        {
            Assert.IsNull(PreviewProtocol.Screen(Request("spell", skillId: "prismatic_orb")));
            Assert.AreEqual("skillId", PreviewProtocol.TargetFieldOf(PreviewAction.Spell),
                "the only field a spell request is REQUIRED to carry is still its id.");
        }

        [Test]
        public void ASpellRequestCarriesTheElementItWasAskedFor()
        {
            var request = Request("spell", skillId: "prismatic_orb", element: "Wind");

            Assert.IsNull(PreviewProtocol.Screen(request));
            Assert.AreEqual("Wind", request.element,
                "the element rides the same request document the skill id does -- a second channel for it " +
                "would be a second thing that can arrive out of step with the id it narrows.");
            Assert.AreEqual("prismatic_orb", PreviewProtocol.TargetIdOf(request, PreviewAction.Spell));
        }

        // THE WRITE RETRY. The watcher publishes its result by File.Replace
        // onto Temp/pp_result.json while preview.ps1 reads that same path
        // every 300ms; a Windows read handle grants no DELETE sharing, so the
        // replace throws IOException whenever a poll lands inside it. The
        // watcher caught that as a warning AFTER consuming the request file,
        // which left the caller waiting out its full timeout for an answer
        // that would never be rewritten. These pin the recovery, not the
        // collision -- the collision needs two processes.

        [Test]
        public void AWriteThatCollidesOnceIsRetriedUntilItLands()
        {
            int attempts = 0;
            var naps = new List<int>();

            PreviewProtocol.WriteWithRetry(
                () =>
                {
                    attempts++;
                    if (attempts < 3) throw new IOException("the process cannot access the file");
                },
                naps.Add);

            Assert.AreEqual(3, attempts, "the write stopped retrying before it succeeded.");
            Assert.AreEqual(2, naps.Count, "one backoff per failed attempt, and none after the one that landed.");
            CollectionAssert.AreEqual(new[] { PreviewProtocol.WriteRetryMs, PreviewProtocol.WriteRetryMs }, naps);
        }

        [Test]
        public void AWriteThatNeverLandsRethrowsSoTheWatcherCanSayWhy()
        {
            int attempts = 0;

            Assert.Throws<IOException>(() =>
                PreviewProtocol.WriteWithRetry(
                    () => { attempts++; throw new IOException("still locked"); },
                    _ => { }));

            Assert.AreEqual(PreviewProtocol.WriteAttempts, attempts,
                "a write that never lands must exhaust exactly WriteAttempts, then surface -- " +
                "silently giving up is the stall this retry exists to end.");
        }

        [Test]
        public void OnlyAnIOExceptionIsRetried()
        {
            int attempts = 0;

            // A serialization failure or a missing Temp/ is not a reader
            // collision, and four more attempts only delay the warning.
            Assert.Throws<InvalidOperationException>(() =>
                PreviewProtocol.WriteWithRetry(
                    () => { attempts++; throw new InvalidOperationException("not a collision"); },
                    _ => Assert.Fail("a non-IO failure must not sleep and retry.")));

            Assert.AreEqual(1, attempts);
        }

        [Test]
        public void EveryActionTheWatcherImplementsIsNamedInTheEnum()
        {
            var actions = new[] { "build", "preview", "spell", "character" }
                .Select(PreviewProtocol.ActionOf)
                .ToList();

            CollectionAssert.DoesNotContain(actions, PreviewAction.Unknown);
            Assert.AreEqual(actions.Count, actions.Distinct().Count(),
                "two action strings resolved to the same PreviewAction, so one of them routes to the wrong work.");
        }
    }
}
