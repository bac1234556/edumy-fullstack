import assert from 'node:assert/strict';
import { detectLessonResourceType, normalizeLessonResource, resolveAssetUrl, formatBytes, getFileExtension } from './resourceUtils.js';

console.log('Running resourceUtils tests...');

// 1. MIME types
assert.equal(detectLessonResourceType({ contentType: 'video/mp4', resourceUrl: 'file' }), 'video');
assert.equal(detectLessonResourceType({ contentType: 'image/png', resourceUrl: 'file' }), 'image');
assert.equal(detectLessonResourceType({ contentType: 'application/pdf', resourceUrl: 'file' }), 'pdf');

// 2. Extensions & Upper/Lower case & Query params
assert.equal(detectLessonResourceType({ resourceUrl: 'lecture.docx' }), 'document');
assert.equal(detectLessonResourceType({ resourceUrl: 'sheet.xlsx' }), 'document');
assert.equal(detectLessonResourceType({ resourceUrl: 'video.mp4' }), 'video');
assert.equal(detectLessonResourceType({ resourceUrl: 'photo.PNG' }), 'image');
assert.equal(detectLessonResourceType({ resourceUrl: 'archive.bin' }), 'file');
assert.equal(detectLessonResourceType({ resourceUrl: 'lecture.mp4?version=2#t=10' }), 'video');
assert.equal(detectLessonResourceType({ resourceUrl: 'document.PDF?v=1' }), 'pdf');
assert.equal(getFileExtension('image.PNG?query=123'), '.png');

// 3. Null / Empty URL
assert.equal(detectLessonResourceType(null), 'none');
assert.equal(detectLessonResourceType({ resourceUrl: '' }), 'none');
assert.equal(detectLessonResourceType({ resourceUrl: '   ' }), 'none');

// 4. Overriding legacy / generic resourceType metadata with specific extension or MIME
assert.equal(detectLessonResourceType({ resourceType: 'File', resourceUrl: '/uploads/example.png' }), 'image');
assert.equal(detectLessonResourceType({ resourceType: 'File', resourceUrl: '/uploads/lecture.mp4' }), 'video');
assert.equal(detectLessonResourceType({ resourceType: 'File', resourceUrl: '/uploads/document.pdf' }), 'pdf');

// 5. Section 10 required unit tests:
// resourceType Image + fileUrl null + resourceEndpoint -> image
assert.equal(detectLessonResourceType({ resourceType: 'Image', resourceUrl: null, fileName: null }), 'image');

// resourceType Image + originalFileName .png -> image
assert.equal(detectLessonResourceType({ resourceType: 'Image', fileName: 'test.png' }), 'image');

// hasResource true + resourceEndpoint -> normalizeLessonResource should have hasResource: true and not 'none'
const norm1 = normalizeLessonResource({ lessonId: 123, resourceType: 'Image', fileUrl: null, resourceEndpoint: '/api/learning/lessons/123/resource', hasResource: true });
assert.equal(norm1.resourceType, 'image');
assert.notEqual(norm1.resourceType, 'none');
assert.equal(norm1.hasResource, true);
assert.equal(norm1.resourceEndpoint, '/learning/lessons/123/resource');

// Explicit hasResource: false -> normalizeLessonResource should preserve hasResource: false
const norm2 = normalizeLessonResource({ lessonId: 124, resourceType: 'None', fileUrl: null, resourceEndpoint: '/api/learning/lessons/124/resource', hasResource: false });
assert.equal(norm2.resourceType, 'none');
assert.equal(norm2.hasResource, false);

// 6. URL resolution tests
assert.equal(resolveAssetUrl(''), '');
assert.equal(resolveAssetUrl('https://cdn.example.com/video.mp4'), 'https://cdn.example.com/video.mp4');
assert.equal(resolveAssetUrl('/uploads/test.png'), '/uploads/test.png');

// 7. Format bytes tests
assert.equal(formatBytes(0), '');
assert.equal(formatBytes(1024), '1 KB');
assert.equal(formatBytes(185024), '180.7 KB');

console.log('✅ All resourceUtils tests passed!');

