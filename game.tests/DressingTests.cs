using System;
using System.Linq;
using Core.Localization;
using Game.Room;

namespace Game.Tests
{
    // THE PROPS ON THE TABLE, AND WHICH VERSION OF EACH IS ON IT TONIGHT.
    //
    // None of them is welded to the table, which is what lets a campaign ship its own screen and a
    // finished campaign earn you a finer tray. The one thing that must stay true of all of it is
    // the standing rule: a nicer tray is nicer, never better. This file holds the shape that makes
    // that structural rather than a promise - a dressing is a name per prop and there is nowhere
    // on it for a number to go.
    public class DressingTests
    {
        [Fact]
        public void EveryPropOnTheTableHasAWordAndAKey()
        {
            foreach (TableProp prop in Enum.GetValues<TableProp>())
            {
                Assert.False(string.IsNullOrWhiteSpace(prop.Word()));
                Assert.True(KeyConventions.IsWellFormed(TableProps.NameKey(prop)));
            }
        }

        [Fact]
        public void TheWordsAreDerivedFromTheEnum_NeverListed()
        {
            Assert.Equal(Enum.GetValues<TableProp>().Length, TableProps.Words.Count);
            Assert.Equal(Enum.GetValues<TableProp>().Length, TableProps.Keys().Count());
        }

        [Fact]
        public void EveryWordReadsBackAsItsOwnProp()
        {
            foreach (TableProp prop in Enum.GetValues<TableProp>())
            {
                Assert.True(TableProps.TryWord(prop.Word(), out TableProp read));
                Assert.Equal(prop, read);
            }

            Assert.False(TableProps.TryWord("candlestick", out _));
            Assert.False(TableProps.TryWord("", out _));
        }

        // the sheet in the room and the sheet on the table are one object, so they are one key -
        // two would be two names for one thing on one table
        [Fact]
        public void TheSheetIsNamedOnceForTheRoomAndTheTable()
        {
            Assert.Equal(Props.NameKey(Prop.Sheet), TableProps.NameKey(TableProp.Sheet));
        }

        // THE MAT IS THE ONE THAT IS NOT EARNED. It is swapped because you walked somewhere
        [Fact]
        public void TheMatIsSwappedByWalking_NotByEarningIt()
        {
            Assert.False(TableProp.Mat.IsEarned());

            foreach (TableProp prop in Enum.GetValues<TableProp>())
                if (prop != TableProp.Mat) Assert.True(prop.IsEarned());
        }


        // ---- the dressing -----------------------------------------------------------------------

        // A PROP WEARING NOTHING IS A PROP THAT IS NOT THERE, so there is no such state
        [Fact]
        public void EveryPropIsWearingSomethingFromTheStart()
        {
            var dressing = new Dressing();

            foreach (TableProp prop in Enum.GetValues<TableProp>())
            {
                Assert.Equal(Dressing.Plain, dressing.Wearing(prop));
                Assert.True(dressing.IsPlain(prop));
            }

            Assert.Equal(0, dressing.Swapped);
        }

        [Fact]
        public void SwappingAPropChangesThatPropAndNoOther()
        {
            var dressing = new Dressing();

            Assert.True(dressing.Wear(TableProp.Tray, "gamblers"));

            Assert.Equal("gamblers", dressing.Wearing(TableProp.Tray));
            Assert.Equal(1, dressing.Swapped);

            foreach (TableProp prop in Enum.GetValues<TableProp>())
                if (prop != TableProp.Tray) Assert.True(dressing.IsPlain(prop));
        }

        [Fact]
        public void SwappingItForWhatItIsAlreadyWearingChangesNothing()
        {
            var dressing = new Dressing();

            Assert.True(dressing.Wear(TableProp.Screen, "greyhollow"));
            Assert.False(dressing.Wear(TableProp.Screen, "greyhollow"));
        }

        [Fact]
        public void ASkinWithNoNameIsRefused()
        {
            var dressing = new Dressing();

            Assert.False(dressing.Wear(TableProp.Dice, null));
            Assert.False(dressing.Wear(TableProp.Dice, "  "));
            Assert.True(dressing.IsPlain(TableProp.Dice));
        }

        [Fact]
        public void StrippingAPropPutsThePlainOneBack()
        {
            var dressing = new Dressing();

            dressing.Wear(TableProp.Sheet, "leather_folio");

            Assert.True(dressing.Strip(TableProp.Sheet));
            Assert.True(dressing.IsPlain(TableProp.Sheet));
            Assert.False(dressing.Strip(TableProp.Sheet));
        }

        [Fact]
        public void APlainTableIsOneCall()
        {
            var dressing = new Dressing();

            foreach (TableProp prop in Enum.GetValues<TableProp>()) dressing.Wear(prop, "fancy");

            Assert.Equal(Enum.GetValues<TableProp>().Length, dressing.Swapped);

            dressing.Plainly();

            Assert.Equal(0, dressing.Swapped);
        }

        // walked in the enum's order, so a table can be dressed by walking it and nothing is left out
        [Fact]
        public void WhatIsWornCoversEveryPropInOrder()
        {
            var dressing = new Dressing();

            dressing.Wear(TableProp.Tray, "gamblers");

            Assert.Equal(Enum.GetValues<TableProp>(), dressing.Worn.Select(w => w.Key).ToArray());
            Assert.DoesNotContain(dressing.Worn, w => string.IsNullOrEmpty(w.Value));
        }
    }
}
