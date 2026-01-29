using System.Threading.Channels;

namespace WebFileBrowser.Services;

public class BackgroundThumbnailQueue {
    private readonly Channel<string> _queue;

    public BackgroundThumbnailQueue(int capacity) {
        // Capacity should be set based on the expected application load and
        // number of concurrent threads accessing the queue.            
        // BoundedChannelFullMode.Wait will cause calls to WriteAsync() to return a task,
        // which completes only when space became available. This leads to backpressure,
        // in case too many publishers/calls start accumulating.
        var options = new BoundedChannelOptions(capacity) {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<string>(options);
    }

    public async ValueTask EnqueueAsync(string path) {
        if(path == null) {
            throw new ArgumentNullException(nameof(path));
        }

        await _queue.Writer.WriteAsync(path);
    }

    public async ValueTask<string> DequeueAsync(CancellationToken cancellationToken) {
        var path = await _queue.Reader.ReadAsync(cancellationToken);
        return path;
    }

    public int Count() {
        return _queue.Reader.Count;
    }
}