// Global JS functions for WordApp

// Render Plain Text or Markdown content safely and accurately
function renderStoryContent(rawText) {
    if (!rawText) return '';
    const normalized = String(rawText).replace(/\r\n/g, '\n');

    if (typeof marked !== 'undefined') {
        try {
            const rawHtml = marked.parse(normalized, {
                breaks: true,
                gfm: true
            });
            if (typeof DOMPurify !== 'undefined') {
                return DOMPurify.sanitize(rawHtml);
            }
            return rawHtml;
        } catch (e) {
            console.warn("Marked parse error, falling back to plain text renderer:", e);
        }
    }

    // Fallback parser supporting both plain text and basic markdown
    const escaped = normalized
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');

    return escaped
        .split(/\n{2,}/)
        .map(block => {
            const trimmed = block.trim();
            if (!trimmed) return '';
            if (/^#{1,6}\s+/.test(trimmed)) {
                const level = trimmed.match(/^(#{1,6})\s+/)[1].length;
                const text = trimmed.replace(/^#{1,6}\s+/, '');
                return `<h${level}>${formatInlineMarkdown(text)}</h${level}>`;
            }
            if (/^---+$/.test(trimmed)) {
                return '<hr />';
            }
            const lines = trimmed.split('\n').map(formatInlineMarkdown).join('<br />');
            return `<p>${lines}</p>`;
        })
        .join('');
}

function formatInlineMarkdown(text) {
    return text
        .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
        .replace(/\*(.+?)\*/g, '<em>$1</em>')
        .replace(/`(.+?)`/g, '<code>$1</code>');
}

// Extract clean plain text from Markdown/Text (for TTS or plain text excerpts)
function extractPlainTextFromStory(rawText) {
    if (!rawText) return '';
    const html = renderStoryContent(rawText);
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = html;
    return (tempDiv.textContent || tempDiv.innerText || '').trim();
}

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

    // Theme Switcher Logic
    const $toggle = $('#theme-toggle');
    if ($toggle.length) {
        const $icon = $toggle.find('i');

        function updateIcon(theme) {
            if (theme === 'light') {
                $icon.attr('class', 'bi bi-moon-stars-fill');
            } else {
                $icon.attr('class', 'bi bi-sun-fill');
            }
        }

        const currentTheme = document.documentElement.getAttribute('data-theme') || 'dark';
        updateIcon(currentTheme);

        $toggle.click(function() {
            const activeTheme = document.documentElement.getAttribute('data-theme') === 'light' ? 'dark' : 'light';
            document.documentElement.setAttribute('data-theme', activeTheme);
            localStorage.setItem('theme', activeTheme);
            updateIcon(activeTheme);
        });
    }
});
