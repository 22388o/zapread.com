﻿/**/

$(document).ready(function () {
    $(".m_input").summernote({
        callbacks: {
            onImageUpload: function (files) {
                that = $(this);
                sendFile(files[0], that);
            }
        },
        focus: false,
        placeholder: 'Write message...',
        disableDragAndDrop: false,
        dialogsInBody: true,
        toolbar: ['bold', 'italic', 'underline', 'strikethrough', 'fontsize', 'color', 'link'],
        minHeight: 100,
        maxHeight: 600,
        hint: {
            match: /\B@@(\w*)$/,
            search: function (keyword, callback) {
                if (!keyword.length) return callback();
                var msg = JSON.stringify({ 'searchstr': keyword.toString() });
                $.ajax({
                    async: true,
                    url: '/Comment/GetMentions',
                    type: 'POST',
                    contentType: "application/json; charset=utf-8",
                    dataType: 'json',
                    data: msg,
                    error: function () {
                        callback();
                    },
                    success: function (res) {
                        callback(res.users);
                    }
                });
            },
            content: function (item) {
                return $("<span class='badge badge-info userhint'>").html('@@' + item)[0];
            }
        }
    });
    window.scrollTo(0, document.body.scrollHeight + 50);
});

/**
 * @return {any} REST headers
 * */
var getAntiForgeryToken = function () {
    var form = $('#__AjaxAntiForgeryForm');
    var token = $('input[name="__RequestVerificationToken"]', form).val();
    var headers = {};
    headers['__RequestVerificationToken'] = token;
    return headers;
};

/**
 * 
 * @param {any} id: message id
 */
var sendMessage = function (id) {
    var action = "/Messages/SendMessage";
    var dataval = '';
    var dataString = '';
    var messageElement = '#message_input';
    dataval = $(messageElement).summernote('code');
    dataString = JSON.stringify({ id: id, content: dataval, isChat: true });
    $('#chatReply').addClass('sk-loading');
    headers = getAntiForgeryToken();
    $.ajax({
        type: "POST",
        url: action,
        data: dataString,
        dataType: "json",
        headers: headers,
        contentType: "application/json; charset=utf-8",
        success: function (response) {
            if (response.success) {
                $(".m_input").summernote('reset');
                $.ajax({
                    type: "POST",
                    url: "/Messages/GetMessage",
                    data: JSON.stringify({ 'id': response.id, 'userId': chatUserId }),
                    headers: headers,
                    dataType: "json",
                    contentType: "application/json; charset=utf-8",
                    success: function (result) {
                        $("#endMessages").append(result.HTMLString);
                        $('.postTime').each(function (i, e) {
                            var datefn = dateFns.parse($(e).html());
                            // Adjust to local time
                            datefn = dateFns.subMinutes(datefn, (new Date()).getTimezoneOffset());
                            var date = dateFns.format(datefn, "DD MMM YYYY");
                            var time = dateFns.distanceInWordsToNow(datefn);
                            $(e).html('<span>' + time + ' ago - ' + date + '</span>');
                            $(e).css('display', 'inline');
                            $(e).removeClass("postTime");
                        });
                        window.scrollTo(0, document.body.scrollHeight + 10);
                    },
                    error: function (jqXHR, textStatus, errorThrown) {
                        alert("fail");
                    }
                });
                $('#chatReply').removeClass('sk-loading');
            }
            else {
                $('#chatReply').removeClass('sk-loading');
                alert(response.message);
            }
        },
        error: function (jqXHR, textStatus, errorThrown) {
            alert("fail");
        }
    });
};