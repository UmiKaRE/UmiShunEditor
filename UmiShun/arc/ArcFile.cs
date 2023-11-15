
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using UmiShun.Utils;

namespace UmiShun.Arc;

public class ArcFile
{
	public uint Unknown1;
	public uint Unknown2;
	public uint SectionCount => (uint)Root.GetFoldersRecursive().Count()+1;
	public uint FileCount => (uint)Root.GetFilesRecursive().Count();
	public uint ContentStartOffset;

	public ArcFolderEntry Root;


	private ArcFile()
	{

	}

	private ArcFolderEntry ReadFolder(System.IO.BinaryReader reader, long offset)
	{
		reader.BaseStream.Seek(offset, System.IO.SeekOrigin.Begin);
		uint fileCount = reader.ReadUInt32();


		var folder = new ArcFolderEntry();
		folder.Entries = new ArcEntry[fileCount];
		
		
		for (int i = 0; i < fileCount; ++i)
		{
			uint entryType = reader.ReadUInt32();
			uint nameOffset = reader.ReadUInt32();
			uint contentLocation = reader.ReadUInt32();	// only relevant for files, not folder
			uint size = reader.ReadUInt32();	// size for file, offset for folder
			if (size > int.MaxValue)
			{
				throw new Exception("File too big");
			}
			long lastFileEndPosition = reader.BaseStream.Position; // save to get back there

			reader.BaseStream.Seek(nameOffset, System.IO.SeekOrigin.Begin);
			string entryName = reader.ReadSZString().StripEdges();

			if (entryType == 0x0)
			{
				var fileEntry = new ArcFileEntry();
				fileEntry.Name = entryName;
				reader.BaseStream.Seek(contentLocation, System.IO.SeekOrigin.Begin);
				fileEntry.Content = reader.ReadBytes((int)size);
				folder.Entries[i] = fileEntry;
			}
			else if (entryType == 0x80000000)
			{
				var folderEntry = ReadFolder(reader, contentLocation);
				folderEntry.Name = entryName;
				folder.Entries[i] = folderEntry;
			}
			else
			{
				throw new Exception("Unknown entry type");
			}
			
			reader.BaseStream.Seek(lastFileEndPosition, System.IO.SeekOrigin.Begin);
		}
		return folder;
	}


	public void Save(BinaryWriter writer)
	{
		var folders = Root.GetFoldersRecursive().ToList();
		folders.Sort((a, b) => b.path.CompareTo(a.path));	// reverse alphabetical (i think that's what is used in original .arc)
		folders.Insert(0,("",Root));
		
		var files = Root.GetFilesRecursive().ToList();
		files.Sort((a, b) => a.path.CompareTo(b.path));

		List<ArcEntry> allEntries = new();
		allEntries.AddRange(folders.Select(x=>x.entry));
		allEntries.AddRange(files.Select(x=>x.entry));

		// get global info
		int headerSize = (allEntries.Count-1) * 4 * 4 + folders.Count * 4;	// 4 bytes per int, 4 ints per file
		int contentTotalSize = files.Select(x => x.entry is ArcFileEntry fEntry ? fEntry.Content.Length : 0).Aggregate((a, b) => a + b);
		
		// build name list
		Dictionary<ArcEntry, int> entryNameOffsets = new();
		int tmpNameOffset = headerSize + 20;
		string nameList = "";
		foreach (var entry in allEntries)
		{
			if (entry == Root)
				continue;	// root has no name
			entryNameOffsets[entry] = tmpNameOffset;
			nameList += entry.Name + "\0";
			tmpNameOffset += entry.Name.Length + 1;
		}
		byte[] nameListBytes = nameList.ToAsciiBuffer();


		int contentOffset = 4*5 + headerSize + nameListBytes.Length;

		// Write 5 first integers (20 first bytes)
		writer.Seek(0, SeekOrigin.Begin);
		writer.Write(Unknown1);
		writer.Write(Unknown2);
		writer.Write(folders.Count);
		writer.Write(allEntries.Count);
		writer.Write(contentOffset);


		// precompute header offsets
		Dictionary<ArcFolderEntry, int> foldersHeaderOffsets = new();
		int tmpHeaderOffset = 20;
		foreach (var folder in folders)
		{
			foldersHeaderOffsets[folder.entry] = tmpHeaderOffset;
			tmpHeaderOffset += folder.entry.Entries.Length * 4 + 1;
		}

		// Build header and content bytes simultaneously
		byte[] contentBytes = new byte[contentTotalSize];		

		int tmpContentByteOffset = 0;
		for(int folderIdx = 0; folderIdx < folders.Count; ++folderIdx)
		{
			var folder = folders[folderIdx].entry;
			writer.Write(folder.Entries.Length);
			foreach (var entry in folder.Entries)
			{
				if (entry is ArcFileEntry fileEntry)
				{	
					writer.Write(0x0);	// folder flag
					writer.Write(entryNameOffsets[entry]);	// name offset
					writer.Write(tmpContentByteOffset);
					writer.Write(fileEntry.Content.Length);
					fileEntry.Content.CopyTo(contentBytes, tmpContentByteOffset);
					tmpContentByteOffset += fileEntry.Content.Length;
				}
				else if (entry is ArcFolderEntry folderEntry)
				{
					writer.Write(0x00000080);	// folder flag
					writer.Write(entryNameOffsets[entry]);	// name offset
					writer.Write(foldersHeaderOffsets[folderEntry]);	// location of folder header
					writer.Write(0x0);	// size
				}
			}
		}
		

		writer.Write(nameListBytes);
		writer.Write(contentBytes);
	}

	public static ArcFile ReadBinaryStream(System.IO.BinaryReader reader)
	{
		var arc = new ArcFile();
		reader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
		arc.Unknown1 = reader.ReadUInt32();
		arc.Unknown2 = reader.ReadUInt32();
		_ = reader.ReadUInt32();
		_ = reader.ReadUInt32();
		arc.ContentStartOffset = reader.ReadUInt32();


		arc.Root = arc.ReadFolder(reader, reader.BaseStream.Position);
		arc.Root.Name = "";

		// Console.WriteLine("Unknown1: " + arc.Unknown1);
		// Console.WriteLine("Unknown2: " + arc.Unknown2);
		// Console.WriteLine("SectionCount: " + arc.SectionCount);
		// Console.WriteLine("FileCount: " + arc.FileCount);
		// Console.WriteLine("ContentStartOffset: " + arc.ContentStartOffset);

		return arc;
	}
}