﻿/*
 * 
 */
import '../../shared/shared';
import '../../realtime/signalr';
import Swal from 'sweetalert2';

import '../../shared/sharedlast';

/**
 * @description When user clicks to make the selected user an administrator
 * @param {any} uid User Id
 * @param {any} gid Group Id
 * @param {int} grant 1 grants admin, revokes otherwise
 */
export function toggleGroupAdmin(uid, gid, grant) {
    Swal.fire({
        title: "Are you sure?",
        text: "This user will have all the privilages of a group administrator.",
        showCancelButton: true
    }).then(function (result) {
        if (result.value) {
            if (grant === 1) {
                $.post("/Group/UpdateUserMakeAdmin/",
                    { "id": uid, "groupId": gid },
                    function (response) {
                        if (response.success) {
                            Swal.fire("User successfully made administrator.", {
                                icon: "success"
                            });
                        }
                        else {
                            Swal.fire("Error", response.message, "error");
                        }
                    });
            } else {
                $.post("/Group/UpdateUserRevokeAdmin/",
                    { "id": uid, "groupId": gid },
                    function (response) {
                        if (response.success) {
                            Swal.fire("Successfully revoked user administrator rights.", {
                                icon: "success"
                            });
                        }
                        else {
                            Swal.fire("Error", response.message, "error");
                        }
                    });
            }
        } else {
            console.log("cancelled make admin");
        }
    });
}

export function toggleGroupMod(uid, gid, grant) {
    Swal.fire({
        title: "Are you sure?",
        text: "This user will have all the privilages of a group moderator.",
        showCancelButton: true
    }).then(function (result) {
        if (result.value) {
            if (grant === 1) {
                $.post("/Group/UpdateUserMakeMod/",
                    { "id": uid, "groupId": gid },
                    function (response) {
                        if (response.success) {
                            Swal.fire("User successfully made moderator.", {
                                icon: "success"
                            });
                        }
                        else {
                            Swal.fire("Error", response.message, "error");
                        }
                    });
            } else {
                $.post("/Group/UpdateUserRevokeMod/",
                    { "id": uid, "groupId": gid },
                    function (response) {
                        if (response.success) {
                            Swal.fire("Successfully revoked user moderator rights.", {
                                icon: "success"
                            });
                        }
                        else {
                            Swal.fire("Error", response.message, "error");
                        }
                    });
            }
        } else {
            console.log("cancelled make mod");
        }
    });
}

window.toggleGroupAdmin = toggleGroupAdmin;
window.toggleGroupMod = toggleGroupMod;