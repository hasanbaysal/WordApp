// Client-side Grammar Info & Examples Explorer (Free Public APIs, No Persistence)
(function (window, document, $) {
    'use strict';

    if (!$ || !document) return;

    // Volatile in-memory session cache only (cleared on reload, never persisted anywhere)
    const sessionCache = new Map();
    const historyStack = [];
    let activeWord = '';
    let activeCardAudio = { uk: '', us: '' };
    let activeRequestId = 0;
    let activePosFilter = 'all';
    let lastRenderedData = null;
    let currentSpeakingBtn = null;

    // ── Turkish Translations for Parts of Speech ────────────────────────────
    const POS_TR = {
        noun: 'İsim (Noun)',
        verb: 'Fiil (Verb)',
        adjective: 'Sıfat (Adjective)',
        adverb: 'Zarf (Adverb)',
        pronoun: 'Zamir (Pronoun)',
        preposition: 'Edat (Preposition)',
        conjunction: 'Bağlaç (Conjunction)',
        interjection: 'Ünlem (Interjection)',
        exclamation: 'Ünlem (Exclamation)',
        determiner: 'Belirteç (Determiner)',
        article: 'Tanımlık (Article)',
        numeral: 'Sayı (Numeral)',
        number: 'Sayı (Number)',
        phrase: 'Deyim / İfade (Phrase)',
        idiom: 'Deyim (Idiom)',
        proverb: 'Atasözü (Proverb)',
        auxiliary: 'Yardımcı Fiil (Auxiliary)',
        'auxiliary verb': 'Yardımcı Fiil (Auxiliary Verb)',
        modal: 'Modal Fiil (Modal)',
        'modal verb': 'Modal Fiil (Modal Verb)',
        particle: 'Parçacık / Edat (Particle)',
        contraction: 'Kısaltma (Contraction)',
        abbreviation: 'Kısaltma (Abbreviation)',
        prefix: 'Ön Ek (Prefix)',
        suffix: 'Son Ek (Suffix)',
        symbol: 'Sembol (Symbol)',
        other: 'Diğer'
    };

    // ── Turkish Translations for Grammar & Usage Tags ───────────────────────
    const GRAMMAR_TAG_TR = {
        countable: 'Sayılabilir (Countable)',
        uncountable: 'Sayılamaz (Uncountable)',
        transitive: 'Geçişli Fiil (Transitive)',
        intransitive: 'Geçişsiz Fiil (Intransitive)',
        ditransitive: 'Çift Geçişli (Ditransitive)',
        ambitransitive: 'Geçişli / Geçişsiz',
        ergative: 'Etkili / Geçişli-Geçişsiz',
        reflexive: 'Dönüşlü (Reflexive)',
        reciprocal: 'İşteş (Reciprocal)',
        irregular: 'Düzensiz (Irregular)',
        regular: 'Düzenli (Regular)',
        attributive: 'İsimden Önce Kullanılır (Attributive)',
        predicative: 'Yüklem Olarak Kullanılır (Predicative)',
        comparable: 'Karşılaştırılabilir (Comparable)',
        'not-comparable': 'Karşılaştırılamaz',
        'plural-only': 'Yalnızca Çoğul',
        'singular-only': 'Yalnızca Tekil',
        collective: 'Topluluk İsmi',
        auxiliary: 'Yardımcı Fiil',
        modal: 'Modal Fiil',
        copulative: 'Bağlama Fiili (Linking)',
        formal: 'Resmi (Formal)',
        informal: 'Günlük / Samimi (Informal)',
        colloquial: 'Konuşma Dili',
        slang: 'Argo (Slang)',
        idiomatic: 'Deyimsel (Idiomatic)',
        figurative: 'Mecazi (Figurative)',
        literal: 'Gerçek Anlam (Literal)',
        literary: 'Edebi (Literary)',
        poetic: 'Şiirsel',
        archaic: 'Eski Kullanım (Archaic)',
        dated: 'Eskimiş (Dated)',
        obsolete: 'Kullanımdan Kalkmış',
        humorous: 'Esprili',
        ironic: 'İronik',
        pejorative: 'Olumsuz / Küçümseyici',
        derogatory: 'Aşağılayıcı',
        euphemistic: 'Örtmece',
        vulgar: 'Kaba',
        offensive: 'Hakaret İçerikli',
        british: 'İngiliz İng. (UK)',
        uk: 'İngiliz İng. (UK)',
        us: 'Amerikan İng. (US)',
        american: 'Amerikan İng. (US)',
        canada: 'Kanada İng.',
        australia: 'Avustralya İng.',
        'in-the-plural': 'Çoğul Halde',
        'usually-plural': 'Genellikle Çoğul',
        'usually-singular': 'Genellikle Tekil'
    };

    const FORM_TAG_TR = {
        plural: 'Çoğul',
        singular: 'Tekil',
        past: 'Geçmiş Zaman',
        participle: 'Ortaç',
        present: 'Geniş / Şimdiki Zaman',
        'third-person': '3. Tekil Şahıs',
        'first-person': '1. Şahıs',
        'second-person': '2. Şahıs',
        comparative: 'Karşılaştırma (-er / more)',
        superlative: 'Üstünlük (-est / most)',
        gerund: 'İsim-Fiil (-ing)',
        infinitive: 'Mastar',
        simple: 'Basit',
        indicative: 'Bildirme Kipi',
        subjunctive: 'Dilek Kipi',
        imperative: 'Emir Kipi',
        alternative: 'Alternatif Yazım',
        archaic: 'Eski Biçim',
        rare: 'Nadir Biçim',
        'short-form': 'Kısa Biçim',
        possessive: 'İyelik',
        diminutive: 'Küçültme',
        feminine: 'Dişil',
        masculine: 'Eril',
        oblique: 'Nesne Biçimi',
        reflexive: 'Dönüşlü'
    };

    const IMPORTANT_TRAIT_KEYS = new Set([
        'countable', 'uncountable', 'transitive', 'intransitive', 'ditransitive',
        'ambitransitive', 'reflexive', 'irregular', 'attributive', 'predicative',
        'plural-only', 'singular-only', 'collective', 'auxiliary', 'modal',
        'copulative', 'formal', 'informal', 'colloquial', 'slang', 'idiomatic',
        'figurative', 'literary', 'archaic', 'dated', 'british', 'uk', 'us', 'american'
    ]);

    // ── Utilities ───────────────────────────────────────────────────────────
    function escapeHtml(text) {
        return String(text ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function escapeRegExp(str) {
        return String(str).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    function stripHtmlSafe(html) {
        if (!html) return '';
        try {
            const doc = new DOMParser().parseFromString(String(html), 'text/html');
            return (doc.body.textContent || '').replace(/\s+/g, ' ').trim();
        } catch {
            return String(html).replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
        }
    }

    function normalizePos(pos) {
        const clean = String(pos || '').trim().toLowerCase();
        if (!clean) return 'other';
        if (clean === 'exclamation') return 'interjection';
        return clean;
    }

    function translatePos(pos) {
        const key = normalizePos(pos);
        return POS_TR[key] || (key.charAt(0).toUpperCase() + key.slice(1));
    }

    function translateTag(tag) {
        const clean = String(tag || '').trim().toLowerCase().replace(/\s+/g, '-');
        return GRAMMAR_TAG_TR[clean] || null;
    }

    function formatFormTags(tags) {
        if (!Array.isArray(tags) || tags.length === 0) return 'Biçim';
        const lower = tags.map(t => String(t || '').toLowerCase().trim()).filter(Boolean);
        const set = new Set(lower);

        if (set.has('third-person') && set.has('singular') && set.has('present')) {
            return '3. Tekil Şahıs (-s)';
        }
        if (set.has('present') && set.has('participle')) {
            return 'Şimdiki Ortaç (-ing)';
        }
        if (set.has('past') && set.has('participle')) {
            return 'Geçmiş Ortaç (V3)';
        }
        if (set.has('simple') && set.has('past')) {
            return 'Geçmiş Zaman (V2)';
        }
        if (set.has('past') && !set.has('participle')) {
            return 'Geçmiş Zaman (V2)';
        }
        if (set.has('comparative')) {
            return 'Karşılaştırma (-er / more)';
        }
        if (set.has('superlative')) {
            return 'Üstünlük (-est / most)';
        }
        if (set.has('plural') && !set.has('singular')) {
            return 'Çoğul (Plural)';
        }

        const translated = lower
            .filter(t => t !== 'canonical' && t !== 'table-tags' && t !== 'inflection-template' && t !== 'class')
            .map(t => FORM_TAG_TR[t] || t);

        return translated.length ? translated.join(' · ') : 'Biçim';
    }

    function highlightTargetWord(text, targetWord) {
        const safeText = escapeHtml(text);
        const cleanTarget = String(targetWord || '').trim();
        if (!cleanTarget || cleanTarget.length < 2) return safeText;

        try {
            const safePattern = escapeRegExp(escapeHtml(cleanTarget));
            const regex = new RegExp(`\\b(${safePattern}[a-zA-Z']*)\\b`, 'gi');
            return safeText.replace(regex, '<mark class="grammar-word-highlight">$1</mark>');
        } catch {
            return safeText;
        }
    }

    function buildLookupCandidates(rawWord) {
        const trimmed = String(rawWord || '').trim().replace(/\s+/g, ' ');
        if (!trimmed) return [];

        const candidates = [];
        const add = (val) => {
            const v = String(val || '').trim().toLowerCase();
            if (v && !candidates.includes(v)) candidates.push(v);
        };

        // Remove parenthetical hints like "lie (down)" or "bank (river)"
        const withoutParens = trimmed.replace(/\s*\([^)]*\)/g, '').trim();
        add(withoutParens);

        if (trimmed.toLowerCase() !== withoutParens.toLowerCase()) {
            add(trimmed);
        }

        // If verb written as "to achieve"
        if (/^to\s+/i.test(withoutParens)) {
            add(withoutParens.replace(/^to\s+/i, ''));
        }

        // If slash-separated variants like "colour / color"
        if (withoutParens.includes('/')) {
            const firstPart = withoutParens.split('/')[0].trim();
            add(firstPart);
        }

        return candidates;
    }

    async function fetchJsonWithTimeout(url, timeoutMs = 8000) {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), timeoutMs);
        try {
            // Avoid custom headers to prevent CORS preflight issues on public APIs
            const res = await fetch(url, { method: 'GET', signal: controller.signal });
            if (!res.ok) {
                return { ok: false, status: res.status, data: null };
            }
            const data = await res.json();
            return { ok: true, status: res.status, data };
        } catch {
            return { ok: false, status: 0, data: null };
        } finally {
            clearTimeout(timer);
        }
    }

    // ── Individual Free API Adapters ────────────────────────────────────────
    async function fetchFreeDictionaryApi(term) {
        const url = `https://freedictionaryapi.com/api/v1/entries/en/${encodeURIComponent(term)}?translations=true`;
        const res = await fetchJsonWithTimeout(url, 8500);
        if (!res.ok || !res.data || !Array.isArray(res.data.entries) || res.data.entries.length === 0) {
            return null;
        }
        return res.data;
    }

    async function fetchDictionaryApiDev(term) {
        const url = `https://api.dictionaryapi.dev/api/v2/entries/en/${encodeURIComponent(term)}`;
        const res = await fetchJsonWithTimeout(url, 8000);
        if (!res.ok || !Array.isArray(res.data) || res.data.length === 0) {
            return null;
        }
        return res.data;
    }

    async function fetchWiktionaryFallback(term) {
        const url = `https://en.wiktionary.org/api/rest_v1/page/definition/${encodeURIComponent(term)}`;
        const res = await fetchJsonWithTimeout(url, 8000);
        if (!res.ok || !res.data || !Array.isArray(res.data.en) || res.data.en.length === 0) {
            return null;
        }
        return res.data.en;
    }

    async function fetchTatoebaSentences(term) {
        const url = `https://api.tatoeba.org/v1/sentences?q=${encodeURIComponent(term)}&lang=eng&trans:lang=tur&limit=10`;
        const res = await fetchJsonWithTimeout(url, 8000);
        if (!res.ok || !res.data) return [];

        const list = Array.isArray(res.data.data)
            ? res.data.data
            : (Array.isArray(res.data.results) ? res.data.results : []);

        const sentences = [];
        const seenEn = new Set();

        for (const item of list) {
            const enText = String(item.text || '').trim();
            if (!enText || enText.length > 220) continue;
            const normEn = enText.toLowerCase();
            if (seenEn.has(normEn)) continue;

            // Handle both flat arrays [{lang/language, text}] and nested arrays [[{...}]]
            let trText = '';
            const rawTrans = item.translations;
            if (Array.isArray(rawTrans)) {
                const flatTrans = [];
                for (const t of rawTrans) {
                    if (Array.isArray(t)) {
                        flatTrans.push(...t);
                    } else if (t && typeof t === 'object') {
                        flatTrans.push(t);
                    }
                }
                const trMatch = flatTrans.find(t => {
                    const code = String(t.lang || t.language || '').toLowerCase();
                    return (code === 'tur' || code === 'tr') && t.text;
                });
                if (trMatch) {
                    trText = String(trMatch.text).trim();
                }
            }

            seenEn.add(normEn);
            sentences.push({
                en: enText,
                tr: trText,
                source: 'Tatoeba'
            });
        }

        // Prefer sentences that have Turkish translations and concise length
        sentences.sort((a, b) => {
            const aHasTr = a.tr ? 1 : 0;
            const bHasTr = b.tr ? 1 : 0;
            if (aHasTr !== bHasTr) return bHasTr - aHasTr;
            return a.en.length - b.en.length;
        });

        return sentences.slice(0, 6);
    }

    async function fetchDatamuseRelations(term) {
        const [mlRes, trgRes, antRes] = await Promise.all([
            fetchJsonWithTimeout(`https://api.datamuse.com/words?ml=${encodeURIComponent(term)}&max=12`, 6500),
            fetchJsonWithTimeout(`https://api.datamuse.com/words?rel_trg=${encodeURIComponent(term)}&max=12`, 6500),
            fetchJsonWithTimeout(`https://api.datamuse.com/words?rel_ant=${encodeURIComponent(term)}&max=10`, 6500)
        ]);

        const extract = (res) => (res.ok && Array.isArray(res.data))
            ? res.data.map(x => String(x.word || '').trim()).filter(Boolean)
            : [];

        const synonyms = extract(mlRes);
        const collocations = extract(trgRes);
        const antonyms = extract(antRes);

        return {
            ok: synonyms.length > 0 || collocations.length > 0 || antonyms.length > 0,
            synonyms,
            collocations,
            antonyms
        };
    }

    async function fetchMyMemoryTurkish(term) {
        const url = `https://api.mymemory.translated.net/get?q=${encodeURIComponent(term)}&langpair=en|tr`;
        const res = await fetchJsonWithTimeout(url, 6500);
        if (!res.ok || !res.data) return [];

        const results = [];
        const lowerTerm = term.toLowerCase();
        const addTr = (val) => {
            const clean = String(val || '').trim();
            if (!clean || clean.length > 60) return;
            if (clean.toLowerCase() === lowerTerm) return;
            if (/MYMEMORY WARNING/i.test(clean)) return;
            if (!results.some(r => r.toLowerCase() === clean.toLowerCase())) {
                results.push(clean);
            }
        };

        if (res.data.responseData && res.data.responseData.translatedText) {
            addTr(res.data.responseData.translatedText);
        }

        if (Array.isArray(res.data.matches)) {
            res.data.matches
                .filter(m => Number(m.match || 0) >= 0.7 && String(m.segment || '').trim().toLowerCase() === lowerTerm)
                .sort((a, b) => Number(b.match || 0) - Number(a.match || 0))
                .forEach(m => addTr(m.translation));
        }

        return results.slice(0, 4);
    }

    // ── Data Aggregation & Normalization ────────────────────────────────────
    function createEmptyModel(displayWord, lookupTerm) {
        return {
            displayWord,
            lookupTerm,
            phonetics: [],       // { text, label }
            audios: [],          // { url, label }
            trTranslations: [],  // string[]
            traits: [],          // { key, label }
            forms: [],           // { word, pos, posLabel, tagLabel }
            meaningsByPos: {},   // { [pos]: { pos, posLabel, definitions: [], synonyms: [], antonyms: [] } }
            sentences: [],       // { en, tr, source }
            synonyms: [],
            antonyms: [],
            collocations: [],
            sources: []
        };
    }

    function addUniqueString(arr, value, maxLen = 50) {
        const clean = String(value || '').trim();
        if (!clean || clean.length > maxLen) return;
        const low = clean.toLowerCase();
        if (!arr.some(item => item.toLowerCase() === low)) {
            arr.push(clean);
        }
    }

    function ensurePosBucket(model, rawPos) {
        const pos = normalizePos(rawPos);
        if (!model.meaningsByPos[pos]) {
            model.meaningsByPos[pos] = {
                pos,
                posLabel: translatePos(pos),
                definitions: [],
                synonyms: [],
                antonyms: []
            };
        }
        return model.meaningsByPos[pos];
    }

    function addTrait(model, rawTag) {
        const clean = String(rawTag || '').trim().toLowerCase().replace(/\s+/g, '-');
        if (!IMPORTANT_TRAIT_KEYS.has(clean)) return;
        const label = translateTag(clean);
        if (!label) return;
        if (!model.traits.some(t => t.label === label)) {
            model.traits.push({ key: clean, label });
        }
    }

    function addDefinition(bucket, defObj) {
        const text = String(defObj.definition || '').trim();
        if (!text) return;

        // Deduplicate near-identical definitions
        const normKey = text.toLowerCase().replace(/[^a-z0-9]/g, '').slice(0, 65);
        const existing = bucket.definitions.find(d => d._key === normKey);
        if (existing) {
            // Merge examples & tags if new source has extra details
            if (Array.isArray(defObj.examples)) {
                defObj.examples.forEach(ex => addUniqueString(existing.examples, ex, 260));
            }
            if (Array.isArray(defObj.tags)) {
                defObj.tags.forEach(t => addUniqueString(existing.tags, t, 60));
            }
            return;
        }

        const examples = [];
        if (Array.isArray(defObj.examples)) {
            defObj.examples.forEach(ex => addUniqueString(examples, ex, 260));
        }

        const tags = [];
        if (Array.isArray(defObj.tags)) {
            defObj.tags.forEach(t => addUniqueString(tags, t, 60));
        }

        bucket.definitions.push({
            _key: normKey,
            definition: text,
            tags,
            examples,
            subsenses: Array.isArray(defObj.subsenses) ? defObj.subsenses : [],
            synonyms: Array.isArray(defObj.synonyms) ? defObj.synonyms : [],
            antonyms: Array.isArray(defObj.antonyms) ? defObj.antonyms : []
        });
    }

    function mergeFreeDictionaryData(model, data) {
        if (!data || !Array.isArray(data.entries)) return;
        addUniqueString(model.sources, 'FreeDictionaryAPI');

        const seenFormKeys = new Set();

        for (const entry of data.entries) {
            const pos = normalizePos(entry.partOfSpeech);
            const bucket = ensurePosBucket(model, pos);

            // Pronunciations
            if (Array.isArray(entry.pronunciations)) {
                for (const p of entry.pronunciations) {
                    const text = String(p.text || '').trim();
                    if (!text) continue;
                    const tagStr = Array.isArray(p.tags) && p.tags.length ? p.tags.slice(0, 2).join(', ') : '';
                    if (!model.phonetics.some(x => x.text === text)) {
                        model.phonetics.push({ text, label: tagStr });
                    }
                }
            }

            // Inflected forms (grammar conjugations / plural / comparative etc.)
            if (Array.isArray(entry.forms)) {
                for (const f of entry.forms) {
                    const fWord = String(f.word || '').trim();
                    if (!fWord || fWord.length > 60) continue;
                    const rawTags = Array.isArray(f.tags) ? f.tags : [];
                    // Skip meta template tags that are not real inflections
                    if (rawTags.some(t => /table-tags|inflection-template/i.test(String(t)))) continue;
                    if (fWord.toLowerCase() === model.lookupTerm.toLowerCase() && rawTags.includes('canonical')) continue;

                    const tagLabel = formatFormTags(rawTags);
                    const fKey = `${pos}|${fWord.toLowerCase()}|${tagLabel}`;
                    if (seenFormKeys.has(fKey)) continue;
                    seenFormKeys.add(fKey);

                    model.forms.push({
                        word: fWord,
                        pos,
                        posLabel: translatePos(pos),
                        tagLabel
                    });
                }
            }

            // Entry-level synonyms/antonyms
            if (Array.isArray(entry.synonyms)) {
                entry.synonyms.forEach(s => {
                    addUniqueString(bucket.synonyms, s);
                    addUniqueString(model.synonyms, s);
                });
            }
            if (Array.isArray(entry.antonyms)) {
                entry.antonyms.forEach(a => {
                    addUniqueString(bucket.antonyms, a);
                    addUniqueString(model.antonyms, a);
                });
            }

            // Senses
            if (Array.isArray(entry.senses)) {
                for (const sense of entry.senses) {
                    const senseTags = [];
                    if (Array.isArray(sense.tags)) {
                        for (const t of sense.tags) {
                            addTrait(model, t);
                            const trTag = translateTag(t);
                            if (trTag) addUniqueString(senseTags, trTag, 60);
                        }
                    }

                    const examples = [];
                    if (Array.isArray(sense.examples)) {
                        sense.examples.forEach(ex => addUniqueString(examples, ex, 260));
                    }
                    if (Array.isArray(sense.quotes)) {
                        sense.quotes.slice(0, 2).forEach(q => {
                            if (q && q.text) addUniqueString(examples, q.text, 260);
                        });
                    }

                    const senseSyns = [];
                    if (Array.isArray(sense.synonyms)) {
                        sense.synonyms.forEach(s => {
                            addUniqueString(senseSyns, s);
                            addUniqueString(bucket.synonyms, s);
                            addUniqueString(model.synonyms, s);
                        });
                    }

                    const senseAnts = [];
                    if (Array.isArray(sense.antonyms)) {
                        sense.antonyms.forEach(a => {
                            addUniqueString(senseAnts, a);
                            addUniqueString(bucket.antonyms, a);
                            addUniqueString(model.antonyms, a);
                        });
                    }

                    // Turkish translations
                    if (Array.isArray(sense.translations)) {
                        for (const tr of sense.translations) {
                            const code = String(tr?.language?.code || '').toLowerCase();
                            if (code === 'tr' && tr.word) {
                                addUniqueString(model.trTranslations, tr.word, 50);
                            }
                        }
                    }

                    // Subsenses (compact)
                    const subsenses = [];
                    if (Array.isArray(sense.subsenses)) {
                        for (const sub of sense.subsenses.slice(0, 3)) {
                            const subDef = String(sub.definition || '').trim();
                            if (!subDef) continue;
                            const subEx = Array.isArray(sub.examples) && sub.examples.length ? String(sub.examples[0]).trim() : '';
                            subsenses.push({ definition: subDef, example: subEx });
                        }
                    }

                    addDefinition(bucket, {
                        definition: sense.definition,
                        tags: senseTags,
                        examples,
                        subsenses,
                        synonyms: senseSyns,
                        antonyms: senseAnts
                    });
                }
            }
        }
    }

    function mergeDictionaryApiDevData(model, entries) {
        if (!Array.isArray(entries) || entries.length === 0) return;
        addUniqueString(model.sources, 'DictionaryAPI.dev');

        for (const entry of entries) {
            if (entry.phonetic) {
                const ph = String(entry.phonetic).trim();
                if (ph && !model.phonetics.some(x => x.text === ph)) {
                    model.phonetics.push({ text: ph, label: '' });
                }
            }

            if (Array.isArray(entry.phonetics)) {
                for (const p of entry.phonetics) {
                    const phText = String(p.text || '').trim();
                    if (phText && !model.phonetics.some(x => x.text === phText)) {
                        model.phonetics.push({ text: phText, label: '' });
                    }
                    const audioUrl = String(p.audio || '').trim();
                    if (audioUrl && !model.audios.some(a => a.url === audioUrl)) {
                        let label = 'Telaffuz';
                        if (/-uk\.mp3/i.test(audioUrl)) label = 'UK (API)';
                        else if (/-us\.mp3/i.test(audioUrl)) label = 'US (API)';
                        else if (/-au\.mp3/i.test(audioUrl)) label = 'AU (API)';
                        model.audios.push({ url: audioUrl, label });
                    }
                }
            }

            if (Array.isArray(entry.meanings)) {
                for (const m of entry.meanings) {
                    const bucket = ensurePosBucket(model, m.partOfSpeech);

                    if (Array.isArray(m.synonyms)) {
                        m.synonyms.forEach(s => {
                            addUniqueString(bucket.synonyms, s);
                            addUniqueString(model.synonyms, s);
                        });
                    }
                    if (Array.isArray(m.antonyms)) {
                        m.antonyms.forEach(a => {
                            addUniqueString(bucket.antonyms, a);
                            addUniqueString(model.antonyms, a);
                        });
                    }

                    if (Array.isArray(m.definitions)) {
                        for (const d of m.definitions) {
                            const examples = d.example ? [String(d.example).trim()] : [];
                            const syns = Array.isArray(d.synonyms) ? d.synonyms : [];
                            const ants = Array.isArray(d.antonyms) ? d.antonyms : [];
                            syns.forEach(s => addUniqueString(model.synonyms, s));
                            ants.forEach(a => addUniqueString(model.antonyms, a));

                            addDefinition(bucket, {
                                definition: d.definition,
                                tags: [],
                                examples,
                                subsenses: [],
                                synonyms: syns,
                                antonyms: ants
                            });
                        }
                    }
                }
            }
        }
    }

    function mergeWiktionaryData(model, enEntries) {
        if (!Array.isArray(enEntries) || enEntries.length === 0) return;
        addUniqueString(model.sources, 'Wiktionary');

        for (const entry of enEntries) {
            const bucket = ensurePosBucket(model, entry.partOfSpeech);
            if (!Array.isArray(entry.definitions)) continue;

            for (const d of entry.definitions) {
                const cleanDef = stripHtmlSafe(d.definition);
                if (!cleanDef) continue;

                const examples = [];
                if (Array.isArray(d.parsedExamples)) {
                    d.parsedExamples.forEach(pe => {
                        const cleanEx = stripHtmlSafe(pe && pe.example);
                        if (cleanEx) addUniqueString(examples, cleanEx, 260);
                    });
                } else if (Array.isArray(d.examples)) {
                    d.examples.forEach(ex => {
                        const cleanEx = stripHtmlSafe(ex);
                        if (cleanEx) addUniqueString(examples, cleanEx, 260);
                    });
                }

                // Detect common grammar hints inside Wiktionary definition text
                const lowerDef = cleanDef.toLowerCase();
                if (/\(transitive\b/.test(lowerDef)) addTrait(model, 'transitive');
                if (/\(intransitive\b/.test(lowerDef)) addTrait(model, 'intransitive');
                if (/\(countable\b/.test(lowerDef)) addTrait(model, 'countable');
                if (/\(uncountable\b/.test(lowerDef)) addTrait(model, 'uncountable');

                addDefinition(bucket, {
                    definition: cleanDef,
                    tags: [],
                    examples,
                    subsenses: [],
                    synonyms: [],
                    antonyms: []
                });
            }
        }
    }

    async function fetchAllGrammarData(displayWord) {
        const candidates = buildLookupCandidates(displayWord);
        if (candidates.length === 0) {
            return createEmptyModel(displayWord, '');
        }

        let primaryTerm = candidates[0];
        let [freeDictRes, dictDevRes, tatoebaRes, datamuseRes] = await Promise.all([
            fetchFreeDictionaryApi(primaryTerm),
            fetchDictionaryApiDev(primaryTerm),
            fetchTatoebaSentences(primaryTerm),
            fetchDatamuseRelations(primaryTerm)
        ]);

        // If neither primary dictionary found the phrase, try secondary candidate (e.g. stripped "to " or slash variant)
        if (!freeDictRes && !dictDevRes && candidates.length > 1) {
            primaryTerm = candidates[1];
            const [fd2, dd2, tat2, dm2] = await Promise.all([
                fetchFreeDictionaryApi(primaryTerm),
                fetchDictionaryApiDev(primaryTerm),
                tatoebaRes.length ? Promise.resolve(tatoebaRes) : fetchTatoebaSentences(primaryTerm),
                datamuseRes.ok ? Promise.resolve(datamuseRes) : fetchDatamuseRelations(primaryTerm)
            ]);
            freeDictRes = fd2;
            dictDevRes = dd2;
            tatoebaRes = tat2;
            datamuseRes = dm2;
        }

        const model = createEmptyModel(displayWord, primaryTerm);

        if (freeDictRes) mergeFreeDictionaryData(model, freeDictRes);
        if (dictDevRes) mergeDictionaryApiDevData(model, dictDevRes);

        // Fallback to Wiktionary REST API if neither dictionary API returned meanings
        if (Object.keys(model.meaningsByPos).length === 0) {
            const wiktData = await fetchWiktionaryFallback(primaryTerm);
            if (wiktData) mergeWiktionaryData(model, wiktData);
        }

        // Merge Tatoeba bilingual example sentences
        if (Array.isArray(tatoebaRes) && tatoebaRes.length > 0) {
            addUniqueString(model.sources, 'Tatoeba');
            model.sentences = tatoebaRes;
        }

        // Merge Datamuse synonyms, antonyms, collocations
        if (datamuseRes && datamuseRes.ok) {
            addUniqueString(model.sources, 'Datamuse');
            const lowerSelf = primaryTerm.toLowerCase();
            datamuseRes.synonyms.forEach(w => {
                if (w.toLowerCase() !== lowerSelf) addUniqueString(model.synonyms, w);
            });
            datamuseRes.antonyms.forEach(w => {
                if (w.toLowerCase() !== lowerSelf) addUniqueString(model.antonyms, w);
            });
            datamuseRes.collocations.forEach(w => {
                if (w.toLowerCase() !== lowerSelf) addUniqueString(model.collocations, w);
            });
        }

        // If FreeDictionaryAPI had no Turkish translations, fetch quick Turkish translation via MyMemory
        if (model.trTranslations.length === 0 && (Object.keys(model.meaningsByPos).length > 0 || model.sentences.length > 0 || model.synonyms.length > 0)) {
            const trList = await fetchMyMemoryTurkish(primaryTerm);
            if (trList.length > 0) {
                addUniqueString(model.sources, 'MyMemory');
                trList.forEach(t => addUniqueString(model.trTranslations, t, 50));
            }
        }

        // If Tatoeba had fewer than 3 example sentences, supplement sentences section with unique dictionary examples
        if (model.sentences.length < 4) {
            const existingEn = new Set(model.sentences.map(s => s.en.toLowerCase()));
            for (const bucket of Object.values(model.meaningsByPos)) {
                for (const def of bucket.definitions) {
                    for (const ex of def.examples) {
                        if (model.sentences.length >= 6) break;
                        const low = ex.toLowerCase();
                        if (!existingEn.has(low) && ex.length >= 12) {
                            existingEn.add(low);
                            model.sentences.push({
                                en: ex,
                                tr: '',
                                source: bucket.posLabel
                            });
                        }
                    }
                    if (model.sentences.length >= 6) break;
                }
                if (model.sentences.length >= 6) break;
            }
        }

        // Limit chip arrays so the popup stays crisp and scannable
        model.trTranslations = model.trTranslations.slice(0, 5);
        model.synonyms = model.synonyms.slice(0, 14);
        model.antonyms = model.antonyms.slice(0, 10);
        model.collocations = model.collocations.slice(0, 14);
        model.forms = model.forms.slice(0, 18);

        return model;
    }

    // ── Text-to-Speech Helper inside Modal ──────────────────────────────────
    function stopModalSpeech() {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
        }
        if (currentSpeakingBtn) {
            $(currentSpeakingBtn).removeClass('is-speaking');
            currentSpeakingBtn = null;
        }
    }

    function speakEnglishText(text, btnEl) {
        if (!('speechSynthesis' in window) || !text) return;

        // Toggle off if clicking the same active button
        if (currentSpeakingBtn && currentSpeakingBtn === btnEl) {
            stopModalSpeech();
            return;
        }

        stopModalSpeech();
        const utterance = new SpeechSynthesisUtterance(text);
        const voices = window.speechSynthesis.getVoices();
        const preferredVoice =
            voices.find(v => /^en-US/i.test(v.lang)) ||
            voices.find(v => /^en-GB/i.test(v.lang)) ||
            voices.find(v => /^en/i.test(v.lang));
        if (preferredVoice) utterance.voice = preferredVoice;
        utterance.lang = 'en-US';
        utterance.rate = 0.92;

        currentSpeakingBtn = btnEl;
        if (btnEl) $(btnEl).addClass('is-speaking');

        utterance.onend = () => {
            if (currentSpeakingBtn === btnEl) {
                $(btnEl).removeClass('is-speaking');
                currentSpeakingBtn = null;
            }
        };
        utterance.onerror = () => {
            if (currentSpeakingBtn === btnEl) {
                $(btnEl).removeClass('is-speaking');
                currentSpeakingBtn = null;
            }
        };

        window.speechSynthesis.speak(utterance);
    }

    // ── Rendering Functions ─────────────────────────────────────────────────
    function updateBackButton() {
        const $backBtn = $('#grammar-back-btn');
        if (historyStack.length > 0) {
            const prev = historyStack[historyStack.length - 1];
            $('#grammar-back-label').text(prev.word);
            $backBtn.show();
        } else {
            $backBtn.hide();
        }
    }

    function renderHeaderMeta(model) {
        $('#grammarModalWordTitle').text(model.displayWord);

        // Phonetics
        const $phonetics = $('#grammar-phonetics-area').empty();
        model.phonetics.slice(0, 3).forEach(p => {
            const labelHtml = p.label ? `<span class="grammar-phonetic-region">${escapeHtml(p.label)}</span>` : '';
            $phonetics.append(`<span class="grammar-phonetic-pill">${labelHtml}${escapeHtml(p.text)}</span>`);
        });

        // Audio buttons (prefer card's UK/US audio if present, otherwise API audios + TTS button)
        const $audio = $('#grammar-audio-controls').empty();
        if (activeCardAudio.uk) {
            $audio.append(`
                <button type="button" class="btn-audio grammar-audio-pill grammar-play-mp3" data-url="${escapeHtml(activeCardAudio.uk)}" title="İngiliz Telaffuzu">
                    <i class="bi bi-volume-up-fill"></i> UK
                </button>`);
        }
        if (activeCardAudio.us) {
            $audio.append(`
                <button type="button" class="btn-audio grammar-audio-pill grammar-play-mp3" data-url="${escapeHtml(activeCardAudio.us)}" title="Amerikan Telaffuzu">
                    <i class="bi bi-volume-up-fill"></i> US
                </button>`);
        }
        if (!activeCardAudio.uk && !activeCardAudio.us && model.audios.length > 0) {
            model.audios.slice(0, 2).forEach(a => {
                $audio.append(`
                    <button type="button" class="btn-audio grammar-audio-pill grammar-play-mp3" data-url="${escapeHtml(a.url)}" title="Telaffuz Dinle">
                        <i class="bi bi-volume-up-fill"></i> ${escapeHtml(a.label)}
                    </button>`);
            });
        }
        if ('speechSynthesis' in window) {
            $audio.append(`
                <button type="button" class="btn-audio grammar-audio-pill grammar-tts-btn" data-text="${escapeHtml(model.lookupTerm || model.displayWord)}" title="Tarayıcı Sesi ile Dinle">
                    <i class="bi bi-soundwave"></i> Dinle
                </button>`);
        }

        // Turkish translation summary chips
        const $tr = $('#grammar-tr-summary').empty();
        if (model.trTranslations.length > 0) {
            $tr.append(`<span class="grammar-tr-prefix">TR:</span>`);
            model.trTranslations.forEach(t => {
                $tr.append(`<span class="grammar-tr-chip">${escapeHtml(t)}</span>`);
            });
        }

        // Source badges in footer
        const $sources = $('#grammar-sources-badges').empty();
        model.sources.forEach(src => {
            $sources.append(`<span class="grammar-source-pill">${escapeHtml(src)}</span>`);
        });
    }

    function renderOverviewSection(model) {
        const posKeys = Object.keys(model.meaningsByPos);
        const $overview = $('#grammar-overview-section');

        if (posKeys.length === 0 && model.traits.length === 0 && model.forms.length === 0) {
            $overview.hide();
            return;
        }
        $overview.show();

        // Parts of Speech pills
        const $posPills = $('#grammar-pos-pills').empty();
        if (posKeys.length > 0) {
            $('#grammar-pos-summary').show();
            posKeys.forEach(pos => {
                const b = model.meaningsByPos[pos];
                const count = b.definitions.length;
                $posPills.append(`
                    <button type="button" class="grammar-pos-pill grammar-pos-jump" data-pos="${escapeHtml(pos)}">
                        <span>${escapeHtml(b.posLabel)}</span>
                        <span class="grammar-pos-count">${count}</span>
                    </button>`);
            });
        } else {
            $('#grammar-pos-summary').hide();
        }

        // Grammar traits
        const $traitsWrap = $('#grammar-traits-wrapper');
        const $traitsList = $('#grammar-traits-list').empty();
        if (model.traits.length > 0) {
            $traitsWrap.show();
            model.traits.forEach(tr => {
                $traitsList.append(`<span class="grammar-trait-badge"><i class="bi bi-tag-fill"></i> ${escapeHtml(tr.label)}</span>`);
            });
        } else {
            $traitsWrap.hide();
        }

        // Inflected forms
        const $formsWrap = $('#grammar-forms-wrapper');
        const $formsGrid = $('#grammar-forms-grid').empty();
        if (model.forms.length > 0) {
            $formsWrap.show();
            model.forms.forEach(f => {
                $formsGrid.append(`
                    <div class="grammar-form-chip">
                        <span class="grammar-form-meta">${escapeHtml(f.tagLabel)}</span>
                        <span class="grammar-form-word">${escapeHtml(f.word)}</span>
                        <span class="grammar-form-pos">${escapeHtml(f.posLabel.split(' ')[0])}</span>
                    </div>`);
            });
        } else {
            $formsWrap.hide();
        }
    }

    function renderMeaningsSection(model) {
        const posKeys = Object.keys(model.meaningsByPos);
        const $section = $('#grammar-meanings-section');

        if (posKeys.length === 0) {
            $section.hide();
            return;
        }
        $section.show();

        // Build POS filter tabs
        const $tabs = $('#grammar-pos-tabs').empty();
        if (posKeys.length > 1) {
            $tabs.append(`
                <button type="button" class="grammar-pos-tab ${activePosFilter === 'all' ? 'active' : ''}" data-pos="all">
                    Tümü (${posKeys.length})
                </button>`);
            posKeys.forEach(pos => {
                const b = model.meaningsByPos[pos];
                const shortLabel = b.posLabel.split(' (')[0];
                $tabs.append(`
                    <button type="button" class="grammar-pos-tab ${activePosFilter === pos ? 'active' : ''}" data-pos="${escapeHtml(pos)}">
                        ${escapeHtml(shortLabel)}
                    </button>`);
            });
            $tabs.show();
        } else {
            $tabs.hide();
        }

        renderMeaningsCards(model);
    }

    function renderMeaningsCards(model) {
        const $list = $('#grammar-meanings-list').empty();
        const posKeys = Object.keys(model.meaningsByPos).filter(pos => activePosFilter === 'all' || activePosFilter === pos);

        posKeys.forEach(pos => {
            const bucket = model.meaningsByPos[pos];
            const totalDefs = bucket.definitions.length;
            const visibleLimit = 5;

            let defsHtml = '';
            bucket.definitions.forEach((def, idx) => {
                const hiddenClass = idx >= visibleLimit ? 'grammar-def-extra d-none' : '';
                const tagsHtml = def.tags.length
                    ? `<div class="grammar-def-tags">${def.tags.map(t => `<span class="grammar-sense-tag">${escapeHtml(t)}</span>`).join('')}</div>`
                    : '';

                const examplesHtml = def.examples.length
                    ? `<div class="grammar-def-examples">${def.examples.slice(0, 3).map(ex => `
                        <div class="grammar-example-inline">
                            <span class="grammar-example-text">“${highlightTargetWord(ex, model.lookupTerm)}”</span>
                            ${'speechSynthesis' in window ? `<button type="button" class="btn-speak-mini grammar-tts-btn" data-text="${escapeHtml(ex)}" title="Örneği Dinle"><i class="bi bi-volume-up"></i></button>` : ''}
                        </div>`).join('')}</div>`
                    : '';

                const subsHtml = def.subsenses && def.subsenses.length
                    ? `<ul class="grammar-subsense-list">${def.subsenses.map(sub => `
                        <li>
                            <span>${escapeHtml(sub.definition)}</span>
                            ${sub.example ? `<div class="grammar-subsense-ex">“${highlightTargetWord(sub.example, model.lookupTerm)}”</div>` : ''}
                        </li>`).join('')}</ul>`
                    : '';

                defsHtml += `
                    <li class="grammar-def-item ${hiddenClass}">
                        <div class="grammar-def-num">${idx + 1}</div>
                        <div class="grammar-def-content">
                            ${tagsHtml}
                            <div class="grammar-def-text">${escapeHtml(def.definition)}</div>
                            ${examplesHtml}
                            ${subsHtml}
                        </div>
                    </li>`;
            });

            const expandBtnHtml = totalDefs > visibleLimit
                ? `<button type="button" class="btn btn-secondary-custom btn-sm-custom grammar-toggle-more mt-2" data-expanded="false" data-total="${totalDefs}">
                       <i class="bi bi-chevron-down"></i> ${totalDefs - visibleLimit} anlam daha göster
                   </button>`
                : '';

            $list.append(`
                <div class="grammar-meaning-card">
                    <div class="grammar-meaning-card-header">
                        <span class="grammar-meaning-pos-badge">${escapeHtml(bucket.posLabel)}</span>
                        <span class="text-muted small">${totalDefs} anlam</span>
                    </div>
                    <ol class="grammar-def-list">${defsHtml}</ol>
                    ${expandBtnHtml}
                </div>`);
        });
    }

    function renderSentencesSection(model) {
        const $section = $('#grammar-sentences-section');
        const $list = $('#grammar-sentences-list').empty();

        if (!model.sentences || model.sentences.length === 0) {
            $section.hide();
            return;
        }
        $section.show();

        model.sentences.forEach(s => {
            const trHtml = s.tr
                ? `<div class="grammar-sentence-tr"><i class="bi bi-translate"></i> ${escapeHtml(s.tr)}</div>`
                : '';
            const sourceBadge = s.source
                ? `<span class="grammar-sentence-src">${escapeHtml(s.source)}</span>`
                : '';

            $list.append(`
                <div class="grammar-sentence-card">
                    <div class="grammar-sentence-top">
                        <p class="grammar-sentence-en mb-0">${highlightTargetWord(s.en, model.lookupTerm)}</p>
                        ${'speechSynthesis' in window ? `<button type="button" class="btn-speak-mini grammar-tts-btn" data-text="${escapeHtml(s.en)}" title="Cümleyi Dinle"><i class="bi bi-volume-up-fill"></i></button>` : ''}
                    </div>
                    ${trHtml}
                    <div class="grammar-sentence-meta">${sourceBadge}</div>
                </div>`);
        });
    }

    function renderRelationsSection(model) {
        const hasSyns = model.synonyms.length > 0;
        const hasAnts = model.antonyms.length > 0;
        const hasCols = model.collocations.length > 0;

        const $section = $('#grammar-relations-section');
        if (!hasSyns && !hasAnts && !hasCols) {
            $section.hide();
            return;
        }
        $section.show();

        const visibleCols = [hasSyns, hasAnts, hasCols].filter(Boolean).length;
        const colClass = visibleCols === 1 ? 'col-12' : (visibleCols === 2 ? 'col-md-6' : 'col-md-4');

        const fillChips = (colId, listId, items, chipClass) => {
            const $col = $(colId).removeClass('col-12 col-md-6 col-md-4').addClass(colClass);
            const $list = $(listId).empty();
            if (!items.length) {
                $col.hide();
                return;
            }
            $col.show();
            items.forEach(word => {
                $list.append(`
                    <button type="button" class="grammar-word-chip ${chipClass}" data-lookup-word="${escapeHtml(word)}" title="'${escapeHtml(word)}' kelimesinin gramer bilgisine bak">
                        ${escapeHtml(word)}
                    </button>`);
            });
        };

        fillChips('#grammar-synonyms-col', '#grammar-synonyms-list', model.synonyms, 'synonym');
        fillChips('#grammar-antonyms-col', '#grammar-antonyms-list', model.antonyms, 'antonym');
        fillChips('#grammar-collocations-col', '#grammar-collocations-list', model.collocations, 'collocation');
    }

    function renderFullModel(model) {
        lastRenderedData = model;
        activePosFilter = 'all';

        const hasAnyContent =
            Object.keys(model.meaningsByPos).length > 0 ||
            model.sentences.length > 0 ||
            model.synonyms.length > 0 ||
            model.antonyms.length > 0 ||
            model.collocations.length > 0 ||
            model.trTranslations.length > 0;

        renderHeaderMeta(model);
        $('#grammar-loading-state').hide();

        if (!hasAnyContent) {
            $('#grammar-content-state').hide();
            $('#grammar-error-title').text(`"${model.displayWord}" için gramer bilgisi bulunamadı`);
            $('#grammar-error-desc').text(
                'Ücretsiz sözlük kaynaklarında bu kelime veya ifade bulunamadı. Kelimenin yalın/tekil halini yukarıdaki arama kutusundan deneyebilirsiniz.'
            );
            $('#grammar-error-state').show();
            return;
        }

        $('#grammar-error-state').hide();
        renderOverviewSection(model);
        renderMeaningsSection(model);
        renderSentencesSection(model);
        renderRelationsSection(model);
        $('#grammar-content-state').show();
    }

    // ── Controller: Load & Open Modal ───────────────────────────────────────
    async function loadWordIntoModal(word, options = {}) {
        const cleanWord = String(word || '').trim();
        if (!cleanWord) return;

        const forceRefresh = Boolean(options.forceRefresh);
        const pushHistory = Boolean(options.pushHistory);

        if (pushHistory && activeWord && activeWord.toLowerCase() !== cleanWord.toLowerCase()) {
            historyStack.push({
                word: activeWord,
                audio: { ...activeCardAudio }
            });
            if (historyStack.length > 10) historyStack.shift();
        }

        activeWord = cleanWord;
        activeCardAudio = options.audio || { uk: '', us: '' };
        updateBackButton();
        stopModalSpeech();

        // Reset UI to loading state
        $('#grammarModalWordTitle').text(cleanWord);
        $('#grammar-phonetics-area').empty();
        $('#grammar-audio-controls').empty();
        $('#grammar-tr-summary').empty();
        $('#grammar-sources-badges').empty();
        $('#grammar-quick-search-input').val('');
        $('#grammar-error-state').hide();
        $('#grammar-content-state').hide();
        $('#grammar-loading-state').show();

        const reqId = ++activeRequestId;
        const cacheKey = cleanWord.toLowerCase();

        if (!forceRefresh && sessionCache.has(cacheKey)) {
            renderFullModel(sessionCache.get(cacheKey));
            return;
        }

        try {
            const data = await fetchAllGrammarData(cleanWord);
            if (reqId !== activeRequestId) return; // Ignore stale response

            // Store in volatile RAM cache only (cleared on refresh/tab close)
            if (Object.keys(data.meaningsByPos).length > 0 || data.sentences.length > 0) {
                sessionCache.set(cacheKey, data);
            }
            renderFullModel(data);
        } catch (err) {
            if (reqId !== activeRequestId) return;
            console.error('Grammar lookup error:', err);
            $('#grammar-loading-state').hide();
            $('#grammar-content-state').hide();
            $('#grammar-error-title').text('Bağlantı hatası oluştu');
            $('#grammar-error-desc').text('Ücretsiz API kaynaklarına ulaşılırken bir sorun yaşandı. Lütfen tekrar deneyin.');
            $('#grammar-error-state').show();
        }
    }

    function openGrammarModal(word, ukMp3 = '', usMp3 = '') {
        const modalEl = document.getElementById('wordGrammarModal');
        if (!modalEl) return;

        historyStack.length = 0;
        const modalInst = bootstrap.Modal.getOrCreateInstance(modalEl);
        modalInst.show();

        loadWordIntoModal(word, {
            audio: { uk: ukMp3 || '', us: usMp3 || '' },
            pushHistory: false,
            forceRefresh: false
        });
    }

    // ── Event Bindings ──────────────────────────────────────────────────────
    $(document).ready(function () {
        // Trigger from any word card or details page button
        $(document).on('click', '.open-grammar-modal', function () {
            const word = $(this).attr('data-word') || '';
            const uk = $(this).attr('data-uk') || '';
            const us = $(this).attr('data-us') || '';
            if (!word.trim()) return;
            openGrammarModal(word, uk, us);
        });

        // Quick search inside modal
        $('#grammar-quick-search-form').on('submit', function (e) {
            e.preventDefault();
            const q = String($('#grammar-quick-search-input').val() || '').trim();
            if (!q) return;
            loadWordIntoModal(q, { pushHistory: true, audio: { uk: '', us: '' } });
        });

        // Back button to previous word
        $('#grammar-back-btn').on('click', function () {
            if (historyStack.length === 0) return;
            const prev = historyStack.pop();
            loadWordIntoModal(prev.word, { pushHistory: false, audio: prev.audio });
        });

        // Refresh / Retry
        $('#grammar-refresh-btn, #grammar-retry-btn').on('click', function () {
            if (!activeWord) return;
            loadWordIntoModal(activeWord, {
                audio: { ...activeCardAudio },
                pushHistory: false,
                forceRefresh: true
            });
        });

        // Filter by Part of Speech tab or Overview POS pill
        $(document).on('click', '.grammar-pos-tab, .grammar-pos-jump', function () {
            if (!lastRenderedData) return;
            const pos = $(this).attr('data-pos') || 'all';
            activePosFilter = pos;
            $('.grammar-pos-tab').removeClass('active')
                .filter(function () { return $(this).attr('data-pos') === pos; })
                .addClass('active');
            renderMeaningsCards(lastRenderedData);

            if ($(this).hasClass('grammar-pos-jump')) {
                const target = document.getElementById('grammar-meanings-section');
                if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });

        // Expand/collapse extra definitions in a POS card
        $(document).on('click', '.grammar-toggle-more', function () {
            const $btn = $(this);
            const $card = $btn.closest('.grammar-meaning-card');
            const expanded = $btn.attr('data-expanded') === 'true';
            const total = Number($btn.attr('data-total') || 0);
            if (expanded) {
                $card.find('.grammar-def-extra').addClass('d-none');
                $btn.attr('data-expanded', 'false')
                    .html(`<i class="bi bi-chevron-down"></i> ${total - 5} anlam daha göster`);
            } else {
                $card.find('.grammar-def-extra').removeClass('d-none');
                $btn.attr('data-expanded', 'true')
                    .html('<i class="bi bi-chevron-up"></i> Daha az göster');
            }
        });

        // Click any synonym / antonym / collocation chip to explore it
        $(document).on('click', '.grammar-word-chip', function () {
            const targetWord = $(this).attr('data-lookup-word') || '';
            if (!targetWord.trim()) return;
            loadWordIntoModal(targetWord, { pushHistory: true, audio: { uk: '', us: '' } });
        });

        // Play MP3 audio inside modal
        $(document).on('click', '.grammar-play-mp3', function () {
            const url = $(this).attr('data-url');
            if (url && typeof window.playWordAudio === 'function') {
                window.playWordAudio(this, url);
            }
        });

        // Speak text via Web Speech API inside modal
        $(document).on('click', '.grammar-tts-btn', function () {
            const text = $(this).attr('data-text');
            if (text) speakEnglishText(text, this);
        });

        // Stop speech & invalidate pending request on modal close
        $('#wordGrammarModal').on('hidden.bs.modal', function () {
            stopModalSpeech();
            activeRequestId++;
        });
    });

    window.WordGrammarExplorer = {
        open: openGrammarModal
    };
})(window, document, window.jQuery);
