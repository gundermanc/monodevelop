//
// AtkCocoaHelper.cs
//
// Author:
//       Iain Holmes <iain@xamarin.com>
//
// Copyright (c) 2016 Xamarin, Inc
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if MAC
using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
#endif

namespace MonoDevelop.Components.AtkCocoaHelper
{
	// AtkCocoaHelper wraps NSAccessibilityElement to set NSAccessibility properties that aren't supported by Atk
	public static class AtkCocoa
	{
		public enum Actions
		{
			AXCancel,
			AXConfirm,
			AXDecrement,
			AXDelete,
			AXIncrement,
			AXPick,
			AXPress,
			AXRaise,
			AXShowAlternateUI,
			AXShowDefaultUI,
			AXShowMenu
		};

		public enum Roles
		{
			AXButton,
			AXCell,
			AXColumn,
			AXGroup,
			AXImage,
			AXMenuButton,
			AXRadioButton,
			AXRow,
			AXRuler,
			AXSplitGroup,
			AXSplitter,
			AXStaticText,
			AXTabGroup,
			AXTextArea
		};

		public enum SubRoles
		{
			AXCloseButton,
		};

		public struct Range
		{
			public int Location { get; set; }
			public int Length { get; set; }
		}
	}

	public class ActionDelegate
	{
		HashSet<AtkCocoa.Actions> actions = new HashSet<AtkCocoa.Actions> ();

		Atk.Object owner;
		internal Atk.Object Owner {
			set {
				owner = value;

				if (owner.GetType () == typeof (Atk.NoOpObject)) {
					return;
				}

				var signal = GLib.Signal.Lookup (owner, "request-actions", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (RequestActionsHandler));

				signal = GLib.Signal.Lookup (owner, "perform-cancel", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformCancelHandler));
				signal = GLib.Signal.Lookup (owner, "perform-confirm", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformConfirmHandler));
				signal = GLib.Signal.Lookup (owner, "perform-decrement", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformDecrementHandler));
				signal = GLib.Signal.Lookup (owner, "perform-delete", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformDeleteHandler));
				signal = GLib.Signal.Lookup (owner, "perform-increment", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformIncrementHandler));
				signal = GLib.Signal.Lookup (owner, "perform-pick", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformPickHandler));
				signal = GLib.Signal.Lookup (owner, "perform-press", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformPressHandler));
				signal = GLib.Signal.Lookup (owner, "perform-raise", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformRaiseHandler));
				signal = GLib.Signal.Lookup (owner, "perform-show-alternate-ui", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformShowAlternateUIHandler));
				signal = GLib.Signal.Lookup (owner, "perform-show-default-ui", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformShowDefaultUIHandler));
				signal = GLib.Signal.Lookup (owner, "perform-show-menu", typeof (GLib.SignalArgs));
				signal.AddDelegate (new EventHandler<GLib.SignalArgs> (PerformShowMenuHandler));
			}
		}

		public ActionDelegate (Gtk.Widget widget)
		{
			widget.Destroyed += WidgetDestroyed;
			Owner = widget.Accessible;
		}

		void WidgetDestroyed (object sender, EventArgs e)
		{
			FreeActions ();
		}

		// Because the allocated memory is passed to unmanaged code where it cannot be freed
		// we need to keep track of it until the object is finalized, or the actions need to be calculated again
		IntPtr allocatedActionPtr;
		IntPtr [] allocatedActionStrings;

		void FreeActions ()
		{
			if (allocatedActionStrings != null) {
				foreach (var ptr in allocatedActionStrings) {
					Marshal.FreeHGlobal (ptr);
				}
				allocatedActionStrings = null;
			}

			if (allocatedActionPtr != IntPtr.Zero) {
				Marshal.FreeHGlobal (allocatedActionPtr);
				allocatedActionPtr = IntPtr.Zero;
			}
		}

		void RegenerateActions ()
		{
			FreeActions ();

			// +1 so we can add a NULL to terminate the array
			int actionCount = actions.Count + 1;
			IntPtr intPtr = Marshal.AllocHGlobal (actionCount * Marshal.SizeOf<IntPtr> ());
			IntPtr [] actionsPtr = new IntPtr [actionCount];

			int i = 0;
			foreach (var action in actions) {
				actionsPtr [i] = Marshal.StringToHGlobalAnsi (action.ToString ());
				i++;
			}

			// Terminator
			actionsPtr [i] = IntPtr.Zero;

			Marshal.Copy (actionsPtr, 0, intPtr, actionCount);

			allocatedActionStrings = actionsPtr;
			allocatedActionPtr = intPtr;
		}

		void AddAction (AtkCocoa.Actions action)
		{
			if (owner.GetType () == typeof (Atk.NoOpObject)) {
				return;
			}

			actions.Add (action);
			RegenerateActions ();
		}

		void RemoveAction (AtkCocoa.Actions action)
		{
			if (owner.GetType () == typeof (Atk.NoOpObject)) {
				return;
			}

			actions.Remove (action);
			RegenerateActions ();
		}

		void RequestActionsHandler (object sender, GLib.SignalArgs args)
		{
			args.RetVal = allocatedActionPtr;
		}

		void PerformCancelHandler (object sender, GLib.SignalArgs args)
		{
			performCancel?.Invoke (this, args);
		}

		void PerformConfirmHandler (object sender, GLib.SignalArgs args)
		{
			performConfirm?.Invoke (this, args);
		}

		void PerformDecrementHandler (object sender, GLib.SignalArgs args)
		{
			performDecrement?.Invoke (this, args);
		}

		void PerformDeleteHandler (object sender, GLib.SignalArgs args)
		{
			performDelete?.Invoke (this, args);
		}

		void PerformIncrementHandler (object sender, GLib.SignalArgs args)
		{
			performIncrement?.Invoke (this, args);
		}

		void PerformPickHandler (object sender, GLib.SignalArgs args)
		{
			performPick?.Invoke (this, args);
		}

		void PerformPressHandler (object sender, GLib.SignalArgs args)
		{
			performPress?.Invoke (this, args);
		}

		void PerformRaiseHandler (object sender, GLib.SignalArgs args)
		{
			performRaise?.Invoke (this, args);
		}

		void PerformShowAlternateUIHandler (object sender, GLib.SignalArgs args)
		{
			performShowAlternateUI?.Invoke (this, args);
		}

		void PerformShowDefaultUIHandler (object sender, GLib.SignalArgs args)
		{
			performShowDefaultUI?.Invoke (this, args);
		}

		void PerformShowMenuHandler (object sender, GLib.SignalArgs args)
		{
			performShowMenu?.Invoke (this, args);
		}

		event EventHandler performCancel;
		public event EventHandler PerformCancel {
			add {
				performCancel += value;
				AddAction (AtkCocoa.Actions.AXCancel);
			}
			remove {
				performCancel -= value;
				RemoveAction (AtkCocoa.Actions.AXCancel);
			}
		}

		event EventHandler performConfirm;
		public event EventHandler PerformConfirm {
			add {
				performConfirm += value;
				AddAction (AtkCocoa.Actions.AXConfirm);
			}
			remove {
				performConfirm -= value;
				RemoveAction (AtkCocoa.Actions.AXConfirm);
			}
		}
		event EventHandler performDecrement;
		public event EventHandler PerformDecrement {
			add {
				performDecrement += value;
				AddAction (AtkCocoa.Actions.AXDecrement);
			}
			remove {
				performDecrement -= value;
				RemoveAction (AtkCocoa.Actions.AXDecrement);
			}
		}
		event EventHandler performDelete;
		public event EventHandler PerformDelete {
			add {
				performDelete += value;
				AddAction (AtkCocoa.Actions.AXDelete);
			}
			remove {
				performDelete -= value;
				RemoveAction (AtkCocoa.Actions.AXDelete);
			}
		}
		event EventHandler performIncrement;
		public event EventHandler PerformIncrement {
			add {
				performIncrement += value;
				AddAction (AtkCocoa.Actions.AXIncrement);
			}
			remove {
				performIncrement -= value;
				RemoveAction (AtkCocoa.Actions.AXIncrement);
			}
		}
		event EventHandler performPick;
		public event EventHandler PerformPick {
			add {
				performPick += value;
				AddAction (AtkCocoa.Actions.AXPick);
			}
			remove {
				performPick -= value;
				RemoveAction (AtkCocoa.Actions.AXPick);
			}
		}
		event EventHandler performPress;
		public event EventHandler PerformPress {
			add {
				performPress += value;
				AddAction (AtkCocoa.Actions.AXPress);
			}
			remove {
				performPress -= value;
				AddAction (AtkCocoa.Actions.AXPress);
			}
		}
		event EventHandler performRaise;
		public event EventHandler PerformRaise {
			add {
				performRaise += value;
				AddAction (AtkCocoa.Actions.AXRaise);
			}
			remove {
				performRaise -= value;
				RemoveAction (AtkCocoa.Actions.AXRaise);
			}
		}
		event EventHandler performShowAlternateUI;
		public event EventHandler PerformShowAlternateUI {
			add {
				performShowAlternateUI += value;
				AddAction (AtkCocoa.Actions.AXShowAlternateUI);
			}
			remove {
				performShowAlternateUI -= value;
				RemoveAction (AtkCocoa.Actions.AXShowAlternateUI);
			}
		}
		event EventHandler performShowDefaultUI;
		public event EventHandler PerformShowDefaultUI {
			add {
				performShowDefaultUI += value;
				AddAction (AtkCocoa.Actions.AXShowDefaultUI);
			}
			remove {
				performShowDefaultUI -= value;
				RemoveAction (AtkCocoa.Actions.AXShowDefaultUI);
			}
		}
		event EventHandler performShowMenu;
		public event EventHandler PerformShowMenu {
			add {
				performShowMenu += value;
				AddAction (AtkCocoa.Actions.AXShowMenu);
			}
			remove {
				performShowMenu -= value;
				RemoveAction (AtkCocoa.Actions.AXShowMenu);
			}
		}
	}

	// On anything other than Mac this is just a dummy class to prevent needing to have #ifdefs all over the main code
	public interface IAccessibilityElementProxy
	{
		event EventHandler PerformCancel;
		event EventHandler PerformConfirm;
		event EventHandler PerformDecrement;
		event EventHandler PerformDelete;
		event EventHandler PerformIncrement;
		event EventHandler PerformPick;
		event EventHandler PerformPress;
		event EventHandler PerformRaise;
		event EventHandler PerformShowAlternateUI;
		event EventHandler PerformShowDefaultUI;
		event EventHandler PerformShowPopupMenu;

		void SetGtkParent (Gtk.Widget realParent);
		void SetFrameInGtkParent (Gdk.Rectangle frame);
		void AddAccessibleChild (IAccessibilityElementProxy child);
		void SetRole (string role, string description = null);
		void SetRole (AtkCocoa.Roles role, string description = null);
		void SetValue (string value);
		void SetTitle (string title);
		void SetLabel (string label);
		void SetIdentifier (string identifier);
		void SetHelp (string help);
		void SetFrameInParent (Gdk.Rectangle rect);
		void SetHidden (bool hidden);
	}

	public interface IAccessibilityNavigableStaticText
	{
		int NumberOfCharacters { get; }
		int InsertionPointLineNumber { get; }
		string Value { get; }

		// Returns frame in Gtk.Widget parent space.
		Gdk.Rectangle GetFrameForRange (AtkCocoa.Range range);
		int GetLineForIndex (int index);
		AtkCocoa.Range GetRangeForLine (int line);
		string GetStringForRange (AtkCocoa.Range range);
		AtkCocoa.Range GetRangeForIndex (int index);
		AtkCocoa.Range GetStyleRangeForIndex (int index);
		AtkCocoa.Range GetRangeForPosition (Gdk.Point position);
	}

	/*
	public abstract class AtkCellRendererProxy : Atk.Object
	{
		public AccessibilityElementProxy Accessible { get; private set; }
		protected AtkCellRendererProxy ()
		{
			Accessible = new AccessibilityElementProxy ();

			// Set the element as secret data on the Atk.Object so AtkCocoa can do something with it
			AtkCocoaHelper.SetNSAccessibilityElement (this, Accessible);
		}
	}
*/
}