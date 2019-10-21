﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osuTK.Graphics;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Online.Chat;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Extensions.IEnumerableExtensions;

namespace osu.Game.Overlays.Chat
{
    public class DrawableChannel : Container
    {
        public readonly Channel Channel;
        protected ChatLineContainer ChatLineFlow;
        private OsuScrollContainer scroll;

        public DrawableChannel(Channel channel)
        {
            Channel = channel;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Child = scroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    // Some chat lines have effects that slightly protrude to the bottom,
                    // which we do not want to mask away, hence the padding.
                    Padding = new MarginPadding { Bottom = 5 },
                    Child = ChatLineFlow = new ChatLineContainer
                    {
                        Padding = new MarginPadding { Left = 20, Right = 20 },
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                    }
                },
            };

            newMessagesArrived(Channel.Messages);

            Channel.NewMessagesArrived += newMessagesArrived;
            Channel.MessageRemoved += messageRemoved;
            Channel.PendingMessageResolved += pendingMessageResolved;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            scrollToEnd();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Channel.NewMessagesArrived -= newMessagesArrived;
            Channel.MessageRemoved -= messageRemoved;
            Channel.PendingMessageResolved -= pendingMessageResolved;
        }

        protected virtual ChatLine CreateChatLine(Message m) => new ChatLine(m);

        protected virtual Drawable CreateDaySeparator(DateTimeOffset time) => new Container
        {
            Margin = new MarginPadding { Vertical = 5 },
            RelativeSizeAxes = Axes.X,
            Height = 2,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
            }
        };

        private void newMessagesArrived(IEnumerable<Message> newMessages)
        {
            // Add up to last Channel.MAX_HISTORY messages
            var displayMessages = newMessages.Skip(Math.Max(0, newMessages.Count() - Channel.MaxHistory));

            var existingChatLines = getChatLines();

            Message lastMessage = existingChatLines.Any() ? existingChatLines.First().Message : null;

            displayMessages.ForEach(m =>
            {
                if (lastMessage == null || lastMessage.Timestamp.ToLocalTime().Date != m.Timestamp.ToLocalTime().Date)
                    ChatLineFlow.Add(CreateDaySeparator(m.Timestamp));

                ChatLineFlow.Add(CreateChatLine(m));
                lastMessage = m;
            });

            if (scroll.IsScrolledToEnd(10) || !existingChatLines.Any() || newMessages.Any(m => m is LocalMessage))
                scrollToEnd();

            var staleMessages = existingChatLines.Where(c => c.LifetimeEnd == double.MaxValue).ToArray();
            int count = staleMessages.Length - Channel.MaxHistory;

            for (int i = 0; i < count; i++)
            {
                var d = staleMessages[i];
                if (!scroll.IsScrolledToEnd(10))
                    scroll.OffsetScrollPosition(-d.DrawHeight);
                d.Expire();
            }
        }

        private void pendingMessageResolved(Message existing, Message updated)
        {
            var found = getChatLines().LastOrDefault(c => c.Message == existing);

            if (found != null)
            {
                Trace.Assert(updated.Id.HasValue, "An updated message was returned with no ID.");

                ChatLineFlow.Remove(found);
                found.Message = updated;
                ChatLineFlow.Add(found);
            }
        }

        private void messageRemoved(Message removed)
        {
            getChatLines().FirstOrDefault(c => c.Message == removed)?.FadeColour(Color4.Red, 400).FadeOut(600).Expire();
        }

        private IEnumerable<ChatLine> getChatLines() => ChatLineFlow.Children.OfType<ChatLine>();

        private void scrollToEnd() => ScheduleAfterChildren(() => scroll.ScrollToEnd());

        protected class ChatLineContainer : FillFlowContainer
        {
            protected override int Compare(Drawable x, Drawable y)
            {
                if (x is ChatLine && y is ChatLine)
                {
                    var xC = (ChatLine)x;
                    var yC = (ChatLine)y;

                    return xC.Message.CompareTo(yC.Message);
                }
                return base.Compare(x, y);
            }
        }
    }
}
