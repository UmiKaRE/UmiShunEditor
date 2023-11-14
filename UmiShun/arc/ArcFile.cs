
using System;
using Godot;
using UmiShun.Utils;

namespace UmiShun.Arc;

public class ArcFile
{
	public uint Unknown1;
	public uint Unknown2;
	public uint SectionCount;
	public uint FileCount;
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

	public static ArcFile ReadBinaryStream(System.IO.BinaryReader reader)
	{
		var arc = new ArcFile();
		reader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
		arc.Unknown1 = reader.ReadUInt32();
		arc.Unknown2 = reader.ReadUInt32();
		arc.SectionCount = reader.ReadUInt32();
		arc.FileCount = reader.ReadUInt32();
		arc.ContentStartOffset = reader.ReadUInt32();

		Console.WriteLine("Unknown1: " + arc.Unknown1);
		Console.WriteLine("Unknown2: " + arc.Unknown2);
		Console.WriteLine("SectionCount: " + arc.SectionCount);
		Console.WriteLine("FileCount: " + arc.FileCount);
		Console.WriteLine("ContentStartOffset: " + arc.ContentStartOffset);


		arc.Root = arc.ReadFolder(reader, reader.BaseStream.Position);
		arc.Root.Name = "/";

		// for (int i = 0; i < arc.SectionCount; ++i)
		// {
			
		// }

		return arc;
	}
}