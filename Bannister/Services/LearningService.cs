using Bannister.Models;
using SQLite;

namespace Bannister.Services;

/// <summary>
/// Service for managing learning content (books and videos)
/// </summary>
public class LearningService
{
    private readonly DatabaseService _db;

    public LearningService(DatabaseService db)
    {
        _db = db;
    }

    #region Books

    /// <summary>
    /// Get all books for a user, ordered by date added (most recent first)
    /// </summary>
    public async Task<List<LearningBook>> GetBooksAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LearningBook>();
        
        return await conn.Table<LearningBook>()
            .Where(b => b.Username == username)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get books by status
    /// </summary>
    public async Task<List<LearningBook>> GetBooksByStatusAsync(string username, string status)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LearningBook>();
        
        return await conn.Table<LearningBook>()
            .Where(b => b.Username == username && b.Status == status)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Add a new book
    /// </summary>
    public async Task<LearningBook> AddBookAsync(LearningBook book)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LearningBook>();
        
        // Get max sort order
        var existing = await conn.Table<LearningBook>()
            .Where(b => b.Username == book.Username)
            .ToListAsync();
        
        book.SortOrder = existing.Count > 0 ? existing.Max(b => b.SortOrder) + 1 : 0;
        book.CreatedAt = DateTime.Now;
        
        await conn.InsertAsync(book);
        return book;
    }

    /// <summary>
    /// Update a book
    /// </summary>
    public async Task UpdateBookAsync(LearningBook book)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(book);
    }

    /// <summary>
    /// Delete a book
    /// </summary>
    public async Task DeleteBookAsync(int bookId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<LearningBook>(bookId);
    }

    /// <summary>
    /// Mark a book as completed
    /// </summary>
    public async Task CompleteBookAsync(int bookId)
    {
        var conn = await _db.GetConnectionAsync();
        var book = await conn.GetAsync<LearningBook>(bookId);
        if (book != null)
        {
            book.Status = "Completed";
            book.CompletedAt = DateTime.Now;
            await conn.UpdateAsync(book);
        }
    }

    /// <summary>
    /// Move book up in order
    /// </summary>
    public async Task MoveBookUpAsync(string username, int bookId)
    {
        var books = await GetBooksAsync(username);
        var index = books.FindIndex(b => b.Id == bookId);
        
        if (index > 0)
        {
            // Swap with previous
            var conn = await _db.GetConnectionAsync();
            var current = books[index];
            var previous = books[index - 1];
            
            int tempOrder = current.SortOrder;
            current.SortOrder = previous.SortOrder;
            previous.SortOrder = tempOrder;
            
            await conn.UpdateAsync(current);
            await conn.UpdateAsync(previous);
        }
    }

    /// <summary>
    /// Move book down in order
    /// </summary>
    public async Task MoveBookDownAsync(string username, int bookId)
    {
        var books = await GetBooksAsync(username);
        var index = books.FindIndex(b => b.Id == bookId);
        
        if (index >= 0 && index < books.Count - 1)
        {
            // Swap with next
            var conn = await _db.GetConnectionAsync();
            var current = books[index];
            var next = books[index + 1];
            
            int tempOrder = current.SortOrder;
            current.SortOrder = next.SortOrder;
            next.SortOrder = tempOrder;
            
            await conn.UpdateAsync(current);
            await conn.UpdateAsync(next);
        }
    }

    #endregion

    #region Videos

    /// <summary>
    /// Get all videos for a user, ordered by date added (most recent first)
    /// </summary>
    public async Task<List<LearningVideo>> GetVideosAsync(string username)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LearningVideo>();
        
        return await conn.Table<LearningVideo>()
            .Where(v => v.Username == username)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get videos by status
    /// </summary>
    public async Task<List<LearningVideo>> GetVideosByStatusAsync(string username, string status)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LearningVideo>();
        
        return await conn.Table<LearningVideo>()
            .Where(v => v.Username == username && v.Status == status)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Add a new video
    /// </summary>
    public async Task<LearningVideo> AddVideoAsync(LearningVideo video)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<LearningVideo>();
        
        // Get max sort order
        var existing = await conn.Table<LearningVideo>()
            .Where(v => v.Username == video.Username)
            .ToListAsync();
        
        video.SortOrder = existing.Count > 0 ? existing.Max(v => v.SortOrder) + 1 : 0;
        video.CreatedAt = DateTime.Now;
        
        await conn.InsertAsync(video);
        return video;
    }

    /// <summary>
    /// Update a video
    /// </summary>
    public async Task UpdateVideoAsync(LearningVideo video)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(video);
    }

    /// <summary>
    /// Delete a video
    /// </summary>
    public async Task DeleteVideoAsync(int videoId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<LearningVideo>(videoId);
    }

    /// <summary>
    /// Mark a video as completed
    /// </summary>
    public async Task CompleteVideoAsync(int videoId)
    {
        var conn = await _db.GetConnectionAsync();
        var video = await conn.GetAsync<LearningVideo>(videoId);
        if (video != null)
        {
            video.Status = "Completed";
            video.CompletedAt = DateTime.Now;
            await conn.UpdateAsync(video);
        }
    }

    /// <summary>
    /// Move video up in order
    /// </summary>
    public async Task MoveVideoUpAsync(string username, int videoId)
    {
        var videos = await GetVideosAsync(username);
        var index = videos.FindIndex(v => v.Id == videoId);
        
        if (index > 0)
        {
            var conn = await _db.GetConnectionAsync();
            var current = videos[index];
            var previous = videos[index - 1];
            
            int tempOrder = current.SortOrder;
            current.SortOrder = previous.SortOrder;
            previous.SortOrder = tempOrder;
            
            await conn.UpdateAsync(current);
            await conn.UpdateAsync(previous);
        }
    }

    /// <summary>
    /// Move video down in order
    /// </summary>
    public async Task MoveVideoDownAsync(string username, int videoId)
    {
        var videos = await GetVideosAsync(username);
        var index = videos.FindIndex(v => v.Id == videoId);
        
        if (index >= 0 && index < videos.Count - 1)
        {
            var conn = await _db.GetConnectionAsync();
            var current = videos[index];
            var next = videos[index + 1];
            
            int tempOrder = current.SortOrder;
            current.SortOrder = next.SortOrder;
            next.SortOrder = tempOrder;
            
            await conn.UpdateAsync(current);
            await conn.UpdateAsync(next);
        }
    }

    #endregion

    #region Stats

    /// <summary>
    /// Get learning statistics
    /// </summary>
    public async Task<(int totalBooks, int completedBooks, int totalVideos, int completedVideos)> GetStatsAsync(string username)
    {
        var books = await GetBooksAsync(username);
        var videos = await GetVideosAsync(username);
        
        return (
            books.Count,
            books.Count(b => b.Status == "Completed"),
            videos.Count,
            videos.Count(v => v.Status == "Completed")
        );
    }

    #endregion
}
