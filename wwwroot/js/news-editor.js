(() => {
    const form = document.querySelector(".news-admin-editor");
    const content = document.querySelector("#news-content");
    const featuredUrl = document.querySelector("#featured-image-url");
    const thumbnailUrl = document.querySelector("#thumbnail-image-url");
    const fileInput = document.querySelector("#featured-image-file");
    const uploadButton = document.querySelector("#upload-featured-image");
    const status = document.querySelector("#featured-image-status");

    if (!form || !content) return;

    const token = form.querySelector('input[name="__RequestVerificationToken"]').value;
    const encodeWebp = async (file, maximumSize) => {
        if (!file || file.size === 0 || file.size > 10 * 1024 * 1024) {
            throw new Error("Ảnh phải có dung lượng tối đa 10 MB.");
        }
        const bitmap = await createImageBitmap(file);
        try {
            if (bitmap.width * bitmap.height > 40000000) {
                throw new Error("Ảnh vượt quá giới hạn 40 megapixel.");
            }
            const scale = Math.min(1, maximumSize / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement("canvas");
            canvas.width = Math.max(1, Math.round(bitmap.width * scale));
            canvas.height = Math.max(1, Math.round(bitmap.height * scale));
            canvas.getContext("2d").drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            return await new Promise((resolve, reject) => {
                canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error("Không thể chuyển ảnh sang WebP.")), "image/webp", 0.86);
            });
        } finally {
            bitmap.close();
        }
    };

    const createThumbnail = async file => {
        const bitmap = await createImageBitmap(file);
        try {
            if (bitmap.width * bitmap.height > 40000000) {
                throw new Error("Ảnh vượt quá giới hạn 40 megapixel.");
            }
            const canvas = document.createElement("canvas");
            canvas.width = 640;
            canvas.height = 360;
            const scale = Math.max(canvas.width / bitmap.width, canvas.height / bitmap.height);
            const width = bitmap.width * scale;
            const height = bitmap.height * scale;
            canvas.getContext("2d").drawImage(bitmap, (canvas.width - width) / 2, (canvas.height - height) / 2, width, height);
            return await new Promise((resolve, reject) => {
                canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error("Không thể tạo thumbnail.")), "image/webp", 0.8);
            });
        } finally {
            bitmap.close();
        }
    };

    const upload = (url, file, filename, thumbnail) => {
        const data = new FormData();
        data.append("file", file, filename);
        if (thumbnail) data.append("thumbnail", thumbnail, "thumbnail.webp");
        return fetch(url, {
            method: "POST",
            body: data,
            credentials: "same-origin",
            headers: { RequestVerificationToken: token }
        }).then(async response => {
            const result = await response.json();
            if (!response.ok) throw new Error(result.error || "Không thể tải ảnh lên.");
            return result;
        });
    };

    if (window.tinymce) {
        window.tinymce.init({
            selector: "#news-content",
            base_url: "https://cdn.jsdelivr.net/npm/tinymce@6.8.5",
            suffix: ".min",
            license_key: "gpl",
            height: 480,
            menubar: false,
            plugins: "autolink lists link image table code paste",
            toolbar: "undo redo | blocks | bold italic underline | bullist numlist | link image table | removeformat code",
            automatic_uploads: true,
            paste_data_images: true,
            images_file_types: "jpg,jpeg,png,webp",
            images_upload_handler: async blobInfo => {
                const image = await encodeWebp(blobInfo.blob(), 1920);
                const result = await upload("/quan-tri/tin-tuc/tai-anh", image, "image.webp");
                return result.location;
            },
            paste_preprocess: (_plugin, args) => {
                const text = new DOMParser().parseFromString(args.content, "text/html").body.textContent.trim();
                const match = /^https:\/\/[^\s<>"']+\.(?:jpe?g|png|webp)(?:\?[^\s<>"']*)?$/i.exec(text);
                if (match) args.content = `<img src="${match[0].replace(/&/g, "&amp;")}" />`;
            },
            content_style: "body { font-family: sans-serif; font-size: 16px; } img { max-width: 100%; height: auto; }"
        });
    }

    featuredUrl.addEventListener("input", () => {
        thumbnailUrl.value = "";
    });

    uploadButton.addEventListener("click", async () => {
        const file = fileInput.files[0];
        if (!file) {
            status.textContent = "Chọn ảnh trước khi tải lên.";
            return;
        }
        uploadButton.disabled = true;
        status.textContent = "Đang tải ảnh và tạo thumbnail…";
        try {
            const image = await encodeWebp(file, 2400);
            const thumbnail = await createThumbnail(file);
            const result = await upload("/quan-tri/tin-tuc/tai-anh-dai-dien", image, "featured.webp", thumbnail);
            featuredUrl.value = result.imageUrl;
            thumbnailUrl.value = result.thumbnailUrl;
            status.textContent = "Đã tải ảnh và tạo thumbnail.";
        } catch (error) {
            status.textContent = error.message;
        } finally {
            uploadButton.disabled = false;
        }
    });
})();
