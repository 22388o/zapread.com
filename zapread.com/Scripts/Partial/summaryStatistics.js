﻿//
// scripts for _partialSummaryStatistics.cshtml

$(document).ready(function () {
    $.get("/Admin/GetPostStats/", function (data, status) {
        stats = data;
        data3 = [];
        data2 = [];
        data4 = [];
        stats.postStats.forEach(function (s) {
            data3.push([s.TimeStampUtc, s.Count]);
        });

        stats.commentStats.forEach(function (s) {
            data4.push([s.TimeStampUtc, s.Count]);
        });

        stats.spendingStats.forEach(function (s) {
            data2.push([s.TimeStampUtc, s.Count]);
        });

        var dataset = [
            {
                label: "Number of posts",
                data: data3,
                color: "#1ab394",
                bars: {
                    show: true,
                    align: "center",
                    barWidth: 24 * 60 * 60 * 600,
                    lineWidth: 0
                }
            },
            {
                label: "Number of comments",
                data: data4,
                color: "#550000",
                bars: {
                    show: true,
                    align: "center",
                    barWidth: 24 * 60 * 60 * 600 * 0.25,
                    lineWidth: 0
                }
            }, {
                label: "Total spent (Satoshi)",
                data: data2,
                yaxis: 2,
                color: "#464f88",
                lines: {
                    lineWidth: 1,
                    show: true,
                    fill: true,
                    fillColor: {
                        colors: [{
                            opacity: 0.2
                        }, {
                            opacity: 0.2
                        }]
                    }
                },
                splines: {
                    show: false,
                    tension: 0.6,
                    lineWidth: 1,
                    fill: 0.1
                }
            }
        ];

        var options = {
            xaxis: {
                mode: "time",
                tickSize: [3, "day"],
                tickLength: 0,
                axisLabel: "Date",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Arial',
                axisLabelPadding: 10,
                color: "#d5d5d5",
            },
            yaxes: [{
                position: "left",
                max: stats.maxPostComments,
                min: 0,
                color: "#d5d5d5",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: 'Arial',
                axisLabelPadding: 3,
                axisLabel: 'Count'
            }, {
                position: "right",
                min: 0,
                max: stats.maxSpent,
                clolor: "#d5d5d5",
                axisLabelUseCanvas: true,
                axisLabelFontSizePixels: 12,
                axisLabelFontFamily: ' Arial',
                axisLabelPadding: 67,
                axisLabel: 'Satoshi'

            }
            ],
            legend: {
                noColumns: 1,
                labelBoxBorderColor: "#000000",
                position: "nw"
            },
            grid: {
                hoverable: true,
                borderWidth: 0
            }
        };

        $.plot($("#flot-dashboard-chart"), dataset, options);

        $("<div id='tooltip'></div>").css({
            position: "absolute",
            display: "none",
            border: "1px solid #fdd",
            padding: "2px",
            "background-color": "#fee",
            opacity: 0.80
        }).appendTo("body");

        $("#flot-dashboard-chart").bind("plothover", function (event, pos, item) {
            if (item) {
                var x = item.datapoint[0].toFixed(2),
                    y = item.datapoint[1].toFixed(2);

                var dx = new Date(parseInt(x)).toDateString();
                $("#tooltip").html(item.series.label + " of " + dx + " = " + parseInt(y))
                    .css({ top: item.pageY + 5, left: item.pageX + 5 })
                    .fadeIn(200);
            } else {
                $("#tooltip").hide();
            }
        });
    });
});