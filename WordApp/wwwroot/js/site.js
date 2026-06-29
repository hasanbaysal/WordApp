// Global JS functions for WordApp

// Play audio dynamically using global HTML5 audio player
function playWordAudio(button, url) {
    if (!url) {
        alert("Ses dosyası URL'si bulunamadı.");
        return;
    }

    const player = document.getElementById('global-audio-player');
    if (!player) return;

    const $btn = $(button);
    const $icon = $btn.find('i');
    const originalClass = $icon.attr('class');

    // Add loading/spinning effect
    $icon.attr('class', 'bi bi-arrow-clockwise spin-icon');

    player.src = url;
    
    player.onended = function() {
        $icon.attr('class', originalClass);
    };

    player.onerror = function() {
        console.error("Audio failed to load from: " + url);
        $icon.attr('class', 'bi bi-slash-circle text-danger');
        setTimeout(() => {
            $icon.attr('class', originalClass);
        }, 1500);
    };

    player.play().catch(function(error) {
        console.warn("Autoplay block or playback error: ", error);
        $icon.attr('class', originalClass);
    });
}

// Custom style injection for JS animations (like spinning icons)
$(document).ready(function() {
    if ($("#spin-style").length === 0) {
        $("<style id='spin-style'>")
            .prop("type", "text/css")
            .html(`
                @keyframes spin {
                    from { transform: rotate(0deg); }
                    to { transform: rotate(360deg); }
                }
                .spin-icon {
                    display: inline-block;
                    animation: spin 1s linear infinite;
                }
            `)
            .appendTo("head");
    }
});
