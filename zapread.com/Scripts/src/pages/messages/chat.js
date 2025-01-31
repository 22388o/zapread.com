﻿/**
 * 
 * User to user chat
 * 
 * [✓] Don't use jQuery
 * 
 */ 
import '../../shared/shared';                                                   // [✓]
import '../../realtime/signalr';                                                // [✓]
import Quill from 'quill';                                                      // [✓]
import DOMPurify from 'dompurify';
import 'quill/dist/quill.core.css'                                              // [✓]
import 'quill/dist/quill.snow.css'                                              // [✓]
import '../../css/quill/quillchat.css'; // Some custom overrides                // [✓]
import 'quill-mention'; // This auto-registers                                  // [✓]
import ImageResize from 'quill-image-resize-module';                            // [✓]
import { ImageUpload } from 'quill-image-upload';                               // [✓]
import AutoLinks from 'quill-auto-links';                                       // [✓]
import QuillImageDropAndPaste from 'quill-image-drop-and-paste';                // [✓]
import { getAntiForgeryToken } from '../../utility/antiforgery';                // [✓]
import { updatePostTimes } from '../../utility/datetime/posttime';              // [✓]
import { ready } from '../../utility/ready';                                    // [✓]
import { subMinutes, format, parseISO, formatDistanceToNow } from 'date-fns';   // [✓]
import { postJson } from '../../utility/postData';                              // [✓]
import '../../shared/sharedlast';                                               // [✓]

Quill.register('modules/imageUpload', ImageUpload);
Quill.register('modules/autoLinks', AutoLinks);
Quill.register('modules/imageResize', ImageResize);
Quill.register('modules/imageDropAndPaste', QuillImageDropAndPaste)

window.subMinutes = subMinutes;
window.format = format;
window.parseISO = parseISO;
window.formatDistanceToNow = formatDistanceToNow;

updatePostTimes();

var toolbarOptions = {
    container: [
        ['send'],
        ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
        ['blockquote'/*, 'code-block'*/],
        //[{ 'header': 1 }, { 'header': 2 }],               // custom button values
        [{ 'list': 'ordered' }, { 'list': 'bullet' }],
        //[{ 'script': 'sub' }, { 'script': 'super' }],      // superscript/subscript
        //[{ 'indent': '-1' }, { 'indent': '+1' }],          // outdent/indent
        //[{ 'direction': 'rtl' }],                         // text direction
        //[{ 'size': ['small', false, 'large', 'huge'] }],  // custom dropdown
        //[{ 'header': [1, 2, 3, 4, 5, 6, false] }],
        [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
        ['link', 'image' /*,'video'*/ /*,'formula'*/],          // add's image support
        //[{ 'font': [] }],
        //[{ 'align': [] }],
        ['clean']                                         // remove formatting button
    ],
    handlers: {
    //    'cancel': function () { },  //dummy - will be updated
        'save': function () { }   //dummy - will be updated
    }
};

var icons = Quill.import('ui/icons');
icons['send'] = 'Send <i class="fa fa-paper-plane"></i>';

//$(document).ready(function () {
ready(function () {
    var quill = new Quill("#editor-container", {
        modules: {
            imageUpload: {
                url: '/Img/UploadImage/', // server url. If the url is empty then the base64 returns
                method: 'POST', // change query method, default 'POST'
                name: 'file', // custom form name
                withCredentials: true, // withCredentials
                headers: getAntiForgeryToken(), // add custom headers, example { token: 'your-token'}
                //csrf: { token: 'token', hash: '' }, // add custom CSRF
                //customUploader: () => { }, // add custom uploader
                // personalize successful callback and call next function to insert new url to the editor
                callbackOK: (serverResponse, insertURL) => {
                    insertURL('/Img/Content/' + serverResponse.imgId + '/');//serverResponse);
                },
                // personalize failed callback
                callbackKO: serverError => {
                    alert(serverError);
                },
                // optional
                // add callback when a image have been chosen
                checkBeforeSend: (file, next) => {
                    console.log(file);
                    next(file); // go back to component and send to the server
                }
            },
            imageDropAndPaste: {
                // add an custom image handler
                handler: imageHandler
            },
            autoLinks: true,
            imageResize: {},
            //imageRotate: {},
            mention: {
                minChars: 1,
                onSelect: function onSelect(item, insertItem) {
                    item.value = `<span class='userhint'>${item.value}</span>`;
                    insertItem(item);
                },
                allowedChars: /^[A-Za-z\sÅÄÖåäö]*$/,
                mentionDenotationChars: ["@"],
                source: async function (searchTerm, renderList) {
                    const matchedUsers = await suggestUsers(searchTerm);
                    renderList(matchedUsers);
                }
            },
            toolbar: toolbarOptions
        },
        placeholder: 'Write a message...',
        theme: 'snow'
    });

    // submit button clicked
    var sendButton = document.querySelector('.ql-send');
    sendButton.addEventListener('click', function () {
        console.log('send clicked!');
        var userId = document.getElementById("editor-container").getAttribute("data-userid");
        sendMessage(userId);
    });
    window.scrollTo(0, document.body.scrollHeight + 50);
});

export async function suggestUsers(searchTerm) {
    var matchedUsers = [];
    var data = await postData("/Comment/Mentions/", {
        searchstr: searchTerm.toString() // not sure if toString is needed here...
    });
    matchedUsers = data.users;
    return matchedUsers;
}

/**
* Do something to our dropped or pasted image
* @param.imageDataUrl {string} - image's dataURL
* @param.type {string} - image's mime type
* @param.imageData {object} - provided more functions to handle the image
*   - imageData.toBlob() {function} - convert image to a BLOB Object
*   - imageData.toFile(filename) {function} - convert image to a File Object
*   - imageData.minify(options) {function)- minify the image, return a promise
*      - options.maxWidth {number} - specify the max width of the image, default is 800
*      - options.maxHeight {number} - specify the max width of the image, default is 800
*      - options.quality {number} - specify the quality of the image, default is 0.8
*/
function imageHandler(imageDataUrl, type, imageData) {
    var filename = 'pastedImage.png'
    var file = imageData.toFile(filename)

    // generate a form data
    var fd = new FormData()
    fd.append('file', file)

    // upload image
    //postData('/Img/UploadImage/')
    const xhr = new XMLHttpRequest();
    // init http query
    xhr.open('POST', '/Img/UploadImage/', true);
    // add custom headers
    var headers = getAntiForgeryToken();
    for (var index in headers) {
        xhr.setRequestHeader(index, headers[index]);
    }

    // listen callback
    xhr.onload = () => {
        if (xhr.status === 200) {
            var data = JSON.parse(xhr.responseText);
            var index = (quill.getSelection() || {}).index || quill.getLength();
            if (index) {
                quill.insertEmbed(index, 'image', '/Img/Content/' + data.imgId + '/', 'user');
            } else {
                console.log({
                    code: xhr.status,
                    type: xhr.statusText,
                    body: xhr.responseText
                });
            }
        };
    }

    xhr.send(fd);
}

/**
 * 
 * @param {any} id: message id
 */
export function sendMessage(id) {
    console.log('send to ' + id);

    var contentEl = document.getElementById('editor-container').querySelectorAll('.ql-editor').item(0);
    var commentHTML = DOMPurify.sanitize(contentEl.innerHTML);

    if (commentHTML === "<p><br></p>" || commentHTML.replace(" ", "") === "<p></p>") {
        // don't send empty!
        return;
    }

    document.getElementById("chatReply").classList.add("sk-loading");   //$('#chatReply').addClass('sk-loading');

    postJson("/Messages/SendMessage/", {
        id: id,
        content: commentHTML,
        isChat: true
    })
        .then((response) => {
        if (response.success) {
            //$(".m_input").summernote('reset');
            contentEl.innerHTML = "";
            postJson("/Messages/GetMessage/", { 'id': response.id }).then((result) => {
                document.getElementById("endMessages").innerHTML += result.HTMLString;  //$("#endMessages").append(result.HTMLString);
                updatePostTimes();

                //$('.postTime').each(function (i, e) {
                //    var datefn = parseISO($(e).html());
                //    datefn = subMinutes(datefn, (new Date()).getTimezoneOffset());
                //    var date = format(datefn, "dd MMM yyyy");
                //    var time = formatDistanceToNow(datefn, { addSuffix: true });
                //    $(e).html('<span>' + time + ' ago - ' + date + '</span>');
                //    $(e).css('display', 'inline');
                //    $(e).removeClass("postTime");
                //});
                window.scrollTo(0, document.body.scrollHeight + 10);
            });
            //$.ajax({
            //    type: "POST",
            //    url: "/Messages/GetMessage",
            //    data: JSON.stringify({ 'id': response.id }),
            //    headers: getAntiForgeryToken(),
            //    dataType: "json",
            //    contentType: "application/json; charset=utf-8",
            //    success: function (result) {
            //        $("#endMessages").append(result.HTMLString);
            //        $('.postTime').each(function (i, e) {
            //            var datefn = parseISO($(e).html());
            //            datefn = subMinutes(datefn, (new Date()).getTimezoneOffset());
            //            var date = format(datefn, "dd MMM yyyy");
            //            var time = formatDistanceToNow(datefn, { addSuffix: true });
            //            $(e).html('<span>' + time + ' ago - ' + date + '</span>');
            //            $(e).css('display', 'inline');
            //            $(e).removeClass("postTime");
            //        });
            //        window.scrollTo(0, document.body.scrollHeight + 10);
            //    },
            //    error: function (jqXHR, textStatus, errorThrown) {
            //        alert("fail");
            //    }
            //});
            document.getElementById("chatReply").classList.remove("sk-loading");
            //$('#chatReply').removeClass('sk-loading');
        }
        else {
            //$('#chatReply').removeClass('sk-loading');
            document.getElementById("chatReply").classList.remove("sk-loading");
            alert(response.message);
        }
    });

    //$.ajax({
    //    type: "POST",
    //    url: action,
    //    data: dataString,
    //    dataType: "json",
    //    headers: getAntiForgeryToken(),
    //    contentType: "application/json; charset=utf-8",
    //    success: function (response) {
    //        if (response.success) {
    //            $(".m_input").summernote('reset');
    //            $.ajax({
    //                type: "POST",
    //                url: "/Messages/GetMessage",
    //                data: JSON.stringify({ 'id': response.id }),
    //                headers: getAntiForgeryToken(),
    //                dataType: "json",
    //                contentType: "application/json; charset=utf-8",
    //                success: function (result) {
    //                    $("#endMessages").append(result.HTMLString);
    //                    $('.postTime').each(function (i, e) {
    //                        var datefn = parseISO($(e).html());
    //                        datefn = subMinutes(datefn, (new Date()).getTimezoneOffset());
    //                        var date = format(datefn, "dd MMM yyyy");
    //                        var time = formatDistanceToNow(datefn, { addSuffix: true });
    //                        $(e).html('<span>' + time + ' ago - ' + date + '</span>');
    //                        $(e).css('display', 'inline');
    //                        $(e).removeClass("postTime");
    //                    });
    //                    window.scrollTo(0, document.body.scrollHeight + 10);
    //                },
    //                error: function (jqXHR, textStatus, errorThrown) {
    //                    alert("fail");
    //                }
    //            });
    //            $('#chatReply').removeClass('sk-loading');
    //        }
    //        else {
    //            $('#chatReply').removeClass('sk-loading');
    //            alert(response.message);
    //        }
    //    },
    //    error: function (jqXHR, textStatus, errorThrown) {
    //        alert("fail");
    //    }
    //});
}
//window.sendMessage = sendMessage;

/**
 * Loads older chat history and inserts into DOM
 * @param {any} id : User id for other user
 */
export function loadolderchats(id) {
    postJson("/Messages/LoadOlder/", { otherId: ChattingWithId, start: startBlock, blocks: 10 })
    .then((response) => {
        if (response.success) {
            //$("#startMessages").prepend(response.HTMLString); // Insert at the front
            document.getElementById("startMessages").innerHTML = response.HTMLString + document.getElementById("startMessages").innerHTML;
            startBlock += 10;
            updatePostTimes();
        }
        else {
            alert(response.message);
        }
    });

    //$.ajax({
    //    type: "POST",
    //    url: "/Messages/LoadOlder/",
    //    data: JSON.stringify({ otherId: ChattingWithId, start: startBlock, blocks: 10 }),
    //    dataType: "json",
    //    headers: getAntiForgeryToken(),
    //    contentType: "application/json; charset=utf-8",
    //    success: function (response) {
    //        if (response.success) {
    //            $("#startMessages").prepend(response.HTMLString); // Insert at the front
    //            startBlock += 10;
    //            $('.postTime').each(function (i, e) {
    //                var datefn = parseISO($(e).html());
    //                datefn = subMinutes(datefn, (new Date()).getTimezoneOffset());
    //                var date = format(datefn, "dd MMM yyyy");
    //                var time = formatDistanceToNow(datefn, { addSuffix: true });
    //                $(e).html('<span>' + time + ' ago - ' + date + '</span>');
    //                $(e).css('display', 'inline');
    //                $(e).removeClass("postTime");
    //            });
    //        }
    //        else {
    //            alert(response.message);
    //        }
    //    },
    //    error: function (jqXHR, textStatus, errorThrown) {
    //        alert("fail");
    //    }
    //});
}
window.loadolderchats = loadolderchats;