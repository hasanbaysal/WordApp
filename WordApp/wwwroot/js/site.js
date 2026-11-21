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

// Render Markdown with enhanced formatting for Grammar Notes (responsive tables, safe external links, optional TOC headings)
function renderMarkdownEnhanced(rawText, options) {
    const opts = options || {};
    const sanitizedHtml = renderStoryContent(rawText);
    if (!sanitizedHtml) {
        return { html: '', headings: [] };
    }

    const container = document.createElement('div');
    container.innerHTML = sanitizedHtml;

    // Wrap tables in scrollable container for responsive mobile/card display
    container.querySelectorAll('table').forEach(function (table) {
        if (table.parentElement && table.parentElement.classList.contains('md-table-wrapper')) {
            return;
        }
        const wrapper = document.createElement('div');
        wrapper.className = 'md-table-wrapper';
        table.parentNode.insertBefore(wrapper, table);
        wrapper.appendChild(table);
    });

    // Ensure external links open safely in new tab
    container.querySelectorAll('a[href]').forEach(function (link) {
        const href = link.getAttribute('href') || '';
        if (/^https?:\/\//i.test(href)) {
            link.setAttribute('target', '_blank');
            link.setAttribute('rel', 'noopener noreferrer');
        }
    });

    // Make GFM task list checkboxes read-only and styled
    container.querySelectorAll('input[type="checkbox"]').forEach(function (checkbox) {
        checkbox.setAttribute('disabled', 'disabled');
        checkbox.classList.add('md-task-checkbox');
        if (checkbox.parentElement && checkbox.parentElement.tagName === 'LI') {
            checkbox.parentElement.classList.add('md-task-item');
        }
    });

    const headings = [];
    if (opts.collectHeadings) {
        container.querySelectorAll('h1, h2, h3').forEach(function (heading, idx) {
            const text = (heading.textContent || '').trim();
            if (!text) return;
            const id = 'gn-sec-' + (idx + 1);
            heading.setAttribute('id', id);
            headings.push({
                id: id,
                level: parseInt(heading.tagName.substring(1), 10),
                text: text
            });
        });
    }

    return {
        html: container.innerHTML,
        headings: headings
    };
}

function renderMarkdownContent(rawText) {
    return renderMarkdownEnhanced(rawText, { collectHeadings: false }).html;
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

        function updateThemeChrome(theme) {
            if (theme === 'light') {
                $icon.attr('class', 'bi bi-moon-stars-fill');
            } else {
                $icon.attr('class', 'bi bi-sun-fill');
            }

            const themeMeta = document.getElementById('theme-color-meta');
            if (themeMeta) {
                themeMeta.setAttribute('content', theme === 'light' ? '#ffffff' : '#0f121a');
            }
        }

        const currentTheme = document.documentElement.getAttribute('data-theme') || 'dark';
        updateThemeChrome(currentTheme);

        $toggle.click(function() {
            const activeTheme = document.documentElement.getAttribute('data-theme') === 'light' ? 'dark' : 'light';
            document.documentElement.setAttribute('data-theme', activeTheme);
            localStorage.setItem('theme', activeTheme);
            updateThemeChrome(activeTheme);
        });
    }

    // Register PWA Service Worker
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function() {
            navigator.serviceWorker.register('/sw.js').catch(function(err) {
                console.warn('Service worker registration skipped:', err);
            });
        });
    }
});
