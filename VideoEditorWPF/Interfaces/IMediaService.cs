using System.Collections.Generic;
using System.Threading.Tasks;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Interfaces
{
    public interface IMediaService
    {
        Task<MediaFile> LoadMediaFileAsync(string filePath);
        Task<List<MediaFile>> LoadMultipleMediaFilesAsync(string[] filePaths);
    }
}
// Сервис загрузки медиафайлов с асинхронным анализом.
// Генерирует thumbnails (80x60, 1с FFmpeg) + реальную длительность (ffprobe/ffmpeg async).
// Batch-загрузка нескольких файлов. Fallback: синий прямоугольник без FFmpeg.
// Сохраняет thumbs во временную папку с уникальными GUID именами.
