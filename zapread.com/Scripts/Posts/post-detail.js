﻿/**/

$(document).ready(function () {
    // Show the comment input box
    initCommentInput(detailPostId);

    // This opens up the vote modal if user clicked to vote (on incomming)
    if (showVoteDialog) {
        document.addEventListener("voteReady", function (e) {
            // Check if vote is for the post, or a comment - this is done using the anchor hash in the url
            var hash = $(location).attr('hash');
            //alert(hash);
            var el;
            if (hash && hash.split('_')[0] === "#cid") {
                var commentId = hash.split('_')[1];

                if (detailPostVote === 1) {
                    el = $('#uVotec_' + commentId);
                } else {
                    el = $('#dVotec_' + commentId);
                }

                // Show vote modal - vote for comment
                vote(commentId, detailPostVote, 2, 100, el);
            } else if (hash)
            {
                // Bad anchor
                alert('Error voting');
            } else {
                if (detailPostVote === 1) {
                    el = $('#uVote_' + detailPostId);
                } else {
                    el = $('#dVote_' + detailPostId);
                }
                // Show vote modal - vote for post
                vote(detailPostId, detailPostVote, 1, 100, el);
            }
        });
    }
});