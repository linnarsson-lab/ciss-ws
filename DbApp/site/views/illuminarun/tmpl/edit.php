<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid') ;
  $item = $this->illuminarun;

?>
<form enctype="multipart/form-data" action="<?php echo JText::_('?option=com_dbapp&view=illuminarun&layout=save&id='.(int) $searchid); ?>" method="post" name="adminForm" id="admin-form" class="form-validate">
<?php
  if ($searchid > 0) {
    echo "<h1> Edit Illumina run $item->illuminarunid </h1>";
  } else {
    echo "<h1> Add new Illumina run </h1>";
  }
?>
<div class='illuminarun'>
  <fieldset>
    <table>
      <tr>
        <td><nobr>RunId/CellId&nbsp; 
               <?php echo JHTML::tooltip('Typically D...XX, or a number for the old machine'); ?>&nbsp;</nobr></td>
        <td><input type="text" name="illuminarunid" id="illuminarunid" value="<?php if ($searchid > 0) echo $item->illuminarunid; ?>" class="inputbox required" size="40"/></td>
        <td rowspan="5">&nbsp; Run&nbsp;document<br />
           &nbsp; <input type="file" name="uploadedfile" id="uploadedfile" value="" size="40"/><br />
           &nbsp; <?php if ($searchid > 0) {
		          echo "&nbsp; $item->rundocument <br /> &nbsp; "; 
		          echo ($item->fileupload == 1)? '[Uploaded]' : '[Is not uploaded]'; 
			} ?></td>
      </tr>
      <tr>
        <td>Title&nbsp;</td>
        <td><input type="text" name="title" id="title" value="<?php if ($searchid > 0) echo $item->title; ?>" class="inputbox required" size="40"/></td>
      </tr>
      <tr>
        <td>Lab&nbsp;book&nbsp;(page)</td>
        <td><input type="text" name="labbookpage" id="labbookpage" value="<?php if ($searchid > 0) echo $item->labbookpage; ?>" class="inputbox required" size="40"/></td>
      </tr>
      <tr>
        <td>Run&nbsp;date&nbsp;</td>
        <td><input type="text" name="rundate" id="rundate" value="<?php if ($searchid > 0) echo $item->rundate; ?>" class="inputbox required" size="40"/></td>
      </tr>
      <tr>
        <td>Comment&nbsp;</td>
        <td><input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $item->comment; ?>" class="inputbox" size="40"/></td>
      </tr>
    </table>
    <table>
<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "
      <tr>
        <td>User: $item->user </td>
        <td>&nbsp; Creation&nbsp;date: $item->time </td>
      </tr>
      <tr>
        <td colspan='2'>will be replaced by</td>
      </tr>";
}
echo "
      <tr>
        <td>User: $user->username </td>
        <td>&nbsp; Creation&nbsp;date: $today </td>
     </tr>";

?>
    </table>
  </fieldset>
</div>
<br/>
<input type="Submit" name="Submit" value="Save" />
<input type="Submit" name="Submit" value="Cancel" />
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=index.php?option=com_dbapp&view=illuminaruns&Itemid="
         . $itemid . ">Return to list of Illumina runs</a><br/>&nbsp;<br/>";
    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $item->id . '" />';
    }


?>
    <input type="hidden" name="task" value="save" />
    <?php echo JHtml::_('form.token'); ?>
</form>






