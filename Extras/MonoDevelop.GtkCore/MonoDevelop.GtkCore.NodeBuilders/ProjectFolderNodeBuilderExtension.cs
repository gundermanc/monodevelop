//
// ProjectFolderNodeBuilderExtension.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2006 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Components.Commands;
using MonoDevelop.GtkCore.GuiBuilder;

namespace MonoDevelop.GtkCore.NodeBuilders
{
	class ProjectFolderNodeBuilderExtension: NodeBuilderExtension
	{
		public override bool CanBuildNode (Type dataType)
		{
			return typeof(ProjectFolder).IsAssignableFrom (dataType) ||
					typeof(Project).IsAssignableFrom (dataType);
		}
		
		public override Type CommandHandlerType {
			get { return typeof(UserInterfaceCommandHandler); }
		}
		
		public override void GetNodeAttributes (ITreeNavigator treeNavigator, object dataObject, ref NodeAttributes attributes)
		{
			if (treeNavigator.Options ["ShowAllFiles"])
				return;

			ProjectFolder folder = dataObject as ProjectFolder;
			if (folder != null && folder.Project != null) {
				GtkDesignInfo info = GtkCoreService.GetGtkInfo (folder.Project);
				if (info != null && info.GtkGuiFolder == folder.Path)
					attributes |= NodeAttributes.Hidden;
			}
		}
	}
	
	class UserInterfaceCommandHandler: NodeCommandHandler
	{
		[CommandHandler (MonoDevelop.GtkCore.GtkCommands.AddNewDialog)]
		public void AddNewDialogToProject()
		{
			AddNewWindow ("DialogFileTemplate");
		}
		
		[CommandHandler (MonoDevelop.GtkCore.GtkCommands.AddNewWindow)]
		public void AddNewWindowToProject()
		{
			AddNewWindow ("WindowFileTemplate");
		}
		
		[CommandHandler (MonoDevelop.GtkCore.GtkCommands.AddNewWidget)]
		public void AddNewWidgetToProject()
		{
			AddNewWindow ("WidgetFileTemplate");
		}
		
		[CommandHandler (GtkCommands.ImportGladeFile)]
		protected void OnImportGladeFile ()
		{
			Project project = CurrentNode.GetParentDataItem (typeof(Project), true) as Project;
			GuiBuilderService.ImportGladeFile (project);
		}
		
		public void AddNewWindow (string id)
		{
			Project project = CurrentNode.GetParentDataItem (typeof(Project), true) as Project;
			ProjectFolder folder = CurrentNode.GetParentDataItem (typeof(ProjectFolder), true) as ProjectFolder;
			
			string path;
			if (folder != null)
				path = folder.Path;
			else
				path = project.BaseDirectory;

			IdeApp.ProjectOperations.CreateProjectFile (project, path, id);
			
			using (IProgressMonitor m = IdeApp.Workbench.ProgressMonitors.GetSaveProgressMonitor ()) {
				project.Save (m);
			}
			CurrentNode.Expanded = true;
		}
	}
}
