using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UmiShun.Arc;
using UmiShunEditor.Utils;

namespace UmiShunEditor;

public partial class ArcEditor : Control
{
	public ArcFile openedFile = null;

	private Button _openArcButton => GetNode<Button>("%BtnOpen");
	private Button _dumpArcButton => GetNode<Button>("%BtnDump");
	private FileDialog _openArcFileDialog => GetNode<FileDialog>("%OpenArcFileDialog");
	private FileDialog _dumpDialog => GetNode<FileDialog>("%DumpDialog");
	private Tree _tree => GetNode<Tree>("%FileTree");

	private Label _debugLabel => GetNode<Label>("%DebugLabel");

	// map entries to a unique identifier, so that we can retrieve them from the TreeItems
	private BidirectionalDictionary<int, ArcEntry> _entryUidMapping = new ();
	private int _lastFileUid = 0; 

	public override void _Ready()
	{
		_openArcButton.Pressed += OnOpenBtnPressed;
		_openArcFileDialog.FileSelected += OnOpenArcFileDialogFileSelected;

		_dumpArcButton.Pressed += OnDumpBtnPressed;
		_dumpDialog.DirSelected += OnDumpDialogFileSelected;
	}

    private void OnDumpBtnPressed()
    {
        _dumpDialog.Popup();
    }

    private void DumpFiles(string pathTo, ArcFolderEntry folder)
	{
		foreach (var entry in folder.Entries)
		{
			if (entry is ArcFileEntry fileEntry)
			{
				string filePath = pathTo + entry.Name;
				GD.Print($"writing to {filePath}");
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllBytes(filePath, fileEntry.Content);
			}
			else if (entry.Type == ArcEntry.EntryType.Folder)
			{
				DumpFiles(pathTo + entry.Name + "\\", (ArcFolderEntry)entry);
			}
		}
	}

	private string AddFilesToString(string str, string pathTo, ArcFolderEntry folder)
	{
		foreach (var entry in folder.Entries)
		{
			if (entry.Type == ArcEntry.EntryType.File)
			{
				str += pathTo + entry.Name + "\n";
			}
			else if (entry.Type == ArcEntry.EntryType.Folder)
			{
				str = AddFilesToString(str, pathTo + entry.Name + "/", (ArcFolderEntry)entry);
			}
		}
		return str;
	}



	private void GenerateUiTree(ArcFolderEntry folder, TreeItem parent)
	{
		foreach (var entry in folder.Entries)
		{
			var item = _tree.CreateItem(parent);
			_entryUidMapping.Add(_lastFileUid++, entry);

			item.SetText(0, entry.Name);

			if (entry is ArcFileEntry fileEntry)
			{
				var byteCount = (long)fileEntry.Content.Length;
				item.SetText(1, $"{byteCount} ({byteCount.ToSizeKibi(ByteSizeUtils.SizeUnits.KB)} KiB)");
			}
			else if (entry is ArcFolderEntry folderEntry)
			{
				item.SetText(1, $"{folderEntry.Entries.Length} files");
				GenerateUiTree((ArcFolderEntry)entry, item);
			}
		}
	}

	private IEnumerable<TreeItem> IterateTreeItems()
	{
		var root = _tree.GetRoot();
		var entry = root.GetFirstChild();
		while (entry != null)
		{
			yield return entry;
			entry = entry.GetNextInTree();
		}
	}


    private void OnDumpDialogFileSelected(string path)
    {
		GD.Print("Dumping files...");
        DumpFiles(path.Replace("/", "\\") + "\\", openedFile.Root);
		GD.Print("Done!");
    }


    private void OnOpenBtnPressed()
    {
        _openArcFileDialog.Popup();
    }

	private void OnOpenArcFileDialogFileSelected(string path)
	{
		using (var stream = File.Open(path, FileMode.Open))
		{
			using (var reader = new BinaryReader(stream, Encoding.ASCII, false))
			{
				openedFile = ArcFile.ReadBinaryStream(reader);
			}
		}

		_tree.Clear();
		_tree.Columns = 2;
		_tree.ColumnTitlesVisible = true;
		_tree.SetColumnExpand(0, false);
		_tree.SetColumnExpand(1, true);
		_tree.SetColumnTitle(0, "Name");
		_tree.SetColumnTitle(1, "Size");
		_tree.HideRoot = true;
		
		// Regenerate the tree and the mapping
		_entryUidMapping.Clear();
		_lastFileUid = 0;
		GenerateUiTree(openedFile.Root, _tree.CreateItem());
	}

	private void OnTreeItemSelected()
	{
		
	}
}
