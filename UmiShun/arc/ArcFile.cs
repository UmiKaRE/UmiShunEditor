
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using UmiShun.Utils;

namespace UmiShun.Arc;

internal class CustomSortComparer : IComparer<(string path, ArcFolderEntry e)>
{
    public int Compare((string path, ArcFolderEntry e) a, (string path, ArcFolderEntry e) b)
    {
        // Compare in reverse alphabetical order
        int alphabeticalComparison = string.Compare(b.path, a.path);

        // Check if one path is the prefix of the other
        if (b.path.StartsWith(a.path+"/"))
        {
            return -int.MaxValue; // x is a prefix of y, so x should come first
        }
        else if (a.path.StartsWith(b.path+"/"))
        {
            return int.MaxValue; // y is a prefix of x, so y should come first
        }

        return alphabeticalComparison;
    }
}


public class CustomComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        // Compare strings based on characters
        for (int i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            if (x[i] == '_')
            {
                if (y[i] != '_')
                {
                    return 1; // "_" has lower priority, so x should come first
                }
            }
            else if (y[i] == '_')
            {
                return -1; // "_" has lower priority, so y should come first
            }

            int charComparison = x[i].CompareTo(y[i]);
            if (charComparison != 0)
            {
                return charComparison;
            }
        }

        // If one string is a prefix of the other, the shorter string comes first
        return x.Length.CompareTo(y.Length);
    }
}

public class ArcFile
{
	public uint Unknown1 => 0x48434153;
	public uint Unknown2 => 256;
	public uint SectionCount => (uint)Root.GetFoldersRecursive().Count()+1;
	public uint FileCount => (uint)Root.GetFilesRecursive().Count();
	// public uint ContentStartOffset;

	public ArcFolderEntry Root;


	public ArcFile()
	{

	}

	private ArcFolderEntry ReadFolder(BinaryReader reader, long offset)
	{
		reader.BaseStream.Seek(offset, SeekOrigin.Begin);
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

			reader.BaseStream.Seek(nameOffset, SeekOrigin.Begin);
			string entryName = reader.ReadSZString().StripEdges();

			if (entryType == 0x0)
			{
				var fileEntry = new ArcFileEntry();
				fileEntry.Name = entryName;
				reader.BaseStream.Seek(contentLocation, SeekOrigin.Begin);
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
			
			reader.BaseStream.Seek(lastFileEndPosition, SeekOrigin.Begin);
		}
		return folder;
	}


	public void Save(BinaryWriter writer)
	{
		var folders = Root.GetFoldersRecursive().ToList();
		folders.Sort(new CustomSortComparer());
		folders.Insert(0,("",Root));
		GD.Print($"--- FOLDERS ---");
			GD.Print(folders.Select(x=>x.path).Aggregate((a, b) => $"{a} ; {b}"));
		
		var files = Root.GetFilesRecursive().ToList();
		files.Sort((a, b) => -a.path.CompareTo(b.path));

		List<ArcEntry> allEntries = new();
		allEntries.AddRange(folders.Select(x=>x.entry));
		allEntries.AddRange(files.Select(x=>x.entry));

		// get global info
		int headerSize = (allEntries.Count-1) * 4 * 4 + folders.Count * 4;	// 4 bytes per int, 4 ints per file
		int contentTotalSize = files.Select(x => x.entry is ArcFileEntry fEntry ? fEntry.Content.Length : 0).Aggregate((a, b) => a + b);
		int nameListSize = allEntries.Select(x => x.Name.Length + 1).Aggregate((a, b) => a + b) - 1;
		int contentOffset = 4*5 + headerSize + nameListSize;
		
		// precompute header offsets & name list
		Dictionary<ArcFolderEntry, int> foldersHeaderOffsets = new();
		Dictionary<ArcEntry, int> entryNameOffsets = new();
		Dictionary<ArcFileEntry, int> fileContentOffsets = new();
		byte[] contentBytes = new byte[contentTotalSize];		
		
		int tmpHeaderOffset = 20;
		int tmpNameOffset = headerSize + 20;
		string nameList = "";
		int tmpContentBufferOffset = 0;
		foreach (var folder in folders)
		{
			var sortedEntries = folder.entry.Entries.OrderByDescending(x => x.Type).ThenByDescending(x => x.Name, new CustomComparer()).ToList();
			folder.entry.Entries = sortedEntries.ToArray<ArcEntry>();
			GD.Print($"--- {folder.path} ---");
			GD.Print(sortedEntries.Select(x=>x.Name).Aggregate((a, b) => $"{a} ; {b}"));

			foldersHeaderOffsets[folder.entry] = tmpHeaderOffset;
			tmpHeaderOffset += folder.entry.Entries.Length * 4 * 4 + 4;

			foreach (var entry in sortedEntries)
			{
				if (entry == Root)
					continue;	// root has no name
				entryNameOffsets[entry] = tmpNameOffset;
				string toAppend = entry.Name + "\0";
				nameList += toAppend;
				tmpNameOffset += toAppend.Length;
				
				if (entry is ArcFileEntry fileEntry)
				{
					fileEntry.Content.CopyTo(contentBytes, tmpContentBufferOffset);
					fileContentOffsets[fileEntry] = tmpContentBufferOffset + contentOffset;
					tmpContentBufferOffset += fileEntry.Content.Length;
				}
			}
		}
		byte[] nameListBytes = nameList.ToAsciiBuffer();
		if (nameListSize != nameListBytes.Length)
			throw new Exception("Name list size mismatch");

		
		// Write 5 first integers (20 first bytes)
		writer.Seek(0, SeekOrigin.Begin);
		writer.Write(Unknown1);
		writer.Write(Unknown2);
		writer.Write(folders.Count);
		writer.Write(allEntries.Count);
		writer.Write(contentOffset);

		// Build header and content bytes simultaneously


		
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
					writer.Write(fileContentOffsets[fileEntry]);	// location of file content
					writer.Write(fileEntry.Content.Length);
				}
				else if (entry is ArcFolderEntry folderEntry)
				{
					writer.Write(0x80000000);	// folder flag
					writer.Write(entryNameOffsets[entry]);	// name offset
					writer.Write(foldersHeaderOffsets[folderEntry]);	// location of folder header
					writer.Write(0x0);	// size
				}
			}
		}
		

		writer.Write(nameListBytes);
		writer.Write(contentBytes);
	}

	public static ArcFile ReadBinaryStream(BinaryReader reader)
	{
		var arc = new ArcFile();
		reader.BaseStream.Seek(0, SeekOrigin.Begin);
		_ = reader.ReadUInt32();	// Unknown1
		_ = reader.ReadUInt32();	// Unknown2
		_ = reader.ReadUInt32();	// SectionCount
		_ = reader.ReadUInt32();	// FileCount
		_ = reader.ReadUInt32();	// ContentStartOffset


		arc.Root = arc.ReadFolder(reader, reader.BaseStream.Position);
		arc.Root.Name = "";

		// Console.WriteLine("Unknown1: " + arc.Unknown1);
		// Console.WriteLine("Unknown2: " + arc.Unknown2);
		// Console.WriteLine("SectionCount: " + arc.SectionCount);
		// Console.WriteLine("FileCount: " + arc.FileCount);
		// Console.WriteLine("ContentStartOffset: " + arc.ContentStartOffset);

		return arc;
	}


	private static void GenerateFolderFromDiskFolder(ArcFolderEntry folder, string path)
	{
		var dir = new DirectoryInfo(path);
		var files = dir.GetFiles();
		var subDirs = dir.GetDirectories();

		folder.Entries = new ArcEntry[files.Length + subDirs.Length];
		for (int i = 0; i < files.Length; ++i)
		{
			var file = files[i];
			var fileEntry = new ArcFileEntry();
			fileEntry.Name = file.Name;
			fileEntry.Content = File.ReadAllBytes(file.FullName);
			folder.Entries[i] = fileEntry;
		}
		for (int i = 0; i < subDirs.Length; ++i)
		{
			var subDir = subDirs[i];
			var subFolder = new ArcFolderEntry();
			subFolder.Name = subDir.Name;
			folder.Entries[files.Length + i] = subFolder;
			GenerateFolderFromDiskFolder(subFolder, subDir.FullName);
		}
	}

	public static ArcFile CreateFromDisk(string path)
	{
		var rootDir = new DirectoryInfo(path);

		var arc = new ArcFile();
		arc.Root = new ArcFolderEntry();
		arc.Root.Name = "";
		arc.Root.Entries = new ArcEntry[0];
		GenerateFolderFromDiskFolder(arc.Root, path);
		return arc;
	}
}