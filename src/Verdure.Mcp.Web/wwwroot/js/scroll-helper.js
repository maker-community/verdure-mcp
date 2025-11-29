// Infinite scroll helper for Dashboard
let scrollContainer = null;
let dotNetRef = null;
let isLoading = false;
let debounceTimer = null;

window.setupInfiniteScroll = function (container, dotNetReference) {
    if (!container) return;
    
    scrollContainer = container;
    dotNetRef = dotNetReference;
    isLoading = false;
    
    // Use scroll event with debounce for reliable detection
    container.addEventListener('scroll', handleScroll, { passive: true });
};

function handleScroll() {
    if (isLoading || debounceTimer) return;
    
    debounceTimer = setTimeout(() => {
        debounceTimer = null;
        
        const scrollTop = scrollContainer.scrollTop;
        const scrollHeight = scrollContainer.scrollHeight;
        const clientHeight = scrollContainer.clientHeight;
        
        // Trigger when user scrolls to within 200px of bottom
        const threshold = 200;
        const distanceFromBottom = scrollHeight - scrollTop - clientHeight;
        
        if (distanceFromBottom <= threshold && !isLoading) {
            isLoading = true;
            
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnScrollNearBottom')
                    .then(() => {
                        // Add a small delay before allowing next load
                        setTimeout(() => {
                            isLoading = false;
                        }, 500);
                    })
                    .catch(() => {
                        isLoading = false;
                    });
            }
        }
    }, 100);
}

window.cleanupInfiniteScroll = function () {
    if (scrollContainer) {
        scrollContainer.removeEventListener('scroll', handleScroll);
    }
    
    if (debounceTimer) {
        clearTimeout(debounceTimer);
        debounceTimer = null;
    }
    
    scrollContainer = null;
    dotNetRef = null;
    isLoading = false;
};
