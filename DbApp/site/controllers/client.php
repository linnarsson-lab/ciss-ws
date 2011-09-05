<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controller');

class DbAppControllerClient extends JController {

//  function client.new() {
//    JRequest::setVar('view' , 'new');
//    parent::display();
//  }

//  function client.edit() {
//    JRequest::setVar('view' , 'edit');
//    parent::display();
//  }

  public function save() {
    $data = JRequest::get('POST');
    $model = $this->getModel('client');
JError::raiseWarning('500', JText::_('[from site/controllers/client.php]C ' . JRequest::getVar('c')));
    if (!$model->save($data)) { 
      $message = JText::_('Client saved');
    } else {
      $message = JText::_('Client saved');
      $message .= ' [' . $model->getError() . ']';
    }
    $this->setRedirect('index.php?option=com_dpapp', $message);
//    return $message;
  }
//  function client.delete() {
//    JRequest::setVar('view' , 'delete');
//    parent::display();
//  }


}
